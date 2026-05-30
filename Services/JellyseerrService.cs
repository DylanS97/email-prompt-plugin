using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyseerrIntegration.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyseerrIntegration.Services;

/// <summary>
/// Handles communication with the JellySeerr API.
/// </summary>
public class JellyseerrService
{
    private const string ApiKeyHeader = "X-Api-Key";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JellyseerrService> _logger;
    private readonly Dictionary<string, (DateTimeOffset Expires, JellyseerrUser? User)> _cache
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyseerrService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{JellyseerrService}"/> interface.</param>
    public JellyseerrService(IHttpClientFactory httpClientFactory, ILogger<JellyseerrService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Checks whether the given Jellyfin user has set their email in JellySeerr.
    /// Returns NeedsEmail=false if the user cannot be found or on any error.
    /// </summary>
    /// <param name="jellyfinUsername">The Jellyfin username to look up.</param>
    /// <returns>Email prompt status for this user.</returns>
    public async Task<EmailPromptStatusDto> GetUserEmailStatusAsync(string jellyfinUsername)
    {
        var user = await FindUserAsync(jellyfinUsername).ConfigureAwait(false);
        if (user is null)
        {
            _logger.LogWarning(
                "JellySeerr Integration: no JellySeerr account matched Jellyfin user '{Username}' — suppressing prompt",
                jellyfinUsername);
            return new EmailPromptStatusDto { NeedsEmail = false };
        }

        if (!string.IsNullOrWhiteSpace(user.Email) && user.Email.Contains('@', StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "JellySeerr Integration: user '{Username}' has email set in JellySeerr (length {Len}) — suppressing prompt",
                jellyfinUsername,
                user.Email.Length);
            return new EmailPromptStatusDto { NeedsEmail = false };
        }

        _logger.LogInformation(
            "JellySeerr Integration: user '{Username}' has no email in JellySeerr — showing prompt",
            jellyfinUsername);
        return new EmailPromptStatusDto { NeedsEmail = true };
    }

    /// <summary>
    /// Updates the JellySeerr email for the given Jellyfin user via PUT /api/v1/user/{userId}.
    /// Invalidates the cache on success.
    /// </summary>
    /// <param name="jellyfinUsername">The Jellyfin username whose JellySeerr email to update.</param>
    /// <param name="email">The email address to set.</param>
    /// <returns>True if the update succeeded, false otherwise.</returns>
    public async Task<bool> UpdateUserEmailAsync(string jellyfinUsername, string email)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null
            || string.IsNullOrWhiteSpace(config.JellyseerrUrl)
            || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            _logger.LogWarning("JellySeerr Integration: cannot update email — plugin is not configured");
            return false;
        }

        var user = await FindUserAsync(jellyfinUsername).ConfigureAwait(false);
        if (user is null)
        {
            _logger.LogWarning(
                "JellySeerr Integration: cannot update email for '{Username}' — user not found in JellySeerr",
                jellyfinUsername);
            return false;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("JellySeerr");
            client.DefaultRequestHeaders.Add(ApiKeyHeader, config.JellyseerrApiKey);

            var url = $"{config.JellyseerrUrl.TrimEnd('/')}/api/v1/user/{user.Id}/settings/main";
            _logger.LogDebug(
                "JellySeerr Integration: sending POST {Url} for Jellyfin user '{Username}' (JellySeerr ID {UserId})",
                url,
                jellyfinUsername,
                user.Id);

            var body = new JellyseerrUpdateUserRequest { Email = email };
            var response = await client.PostAsJsonAsync(url, body).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning(
                    "JellySeerr Integration: PUT /api/v1/user/{UserId} returned {StatusCode} for '{Username}': {Body}",
                    user.Id,
                    (int)response.StatusCode,
                    jellyfinUsername,
                    content);
                return false;
            }

            _logger.LogInformation(
                "JellySeerr Integration: successfully set email for JellySeerr user {UserId} (Jellyfin: '{Username}')",
                user.Id,
                jellyfinUsername);

            InvalidateCache(jellyfinUsername);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "JellySeerr Integration: exception while updating email for '{Username}'",
                jellyfinUsername);
            return false;
        }
    }

    /// <summary>
    /// Checks whether the given Jellyfin user has an active JellySeerr request for the specified media.
    /// Returns a status object indicating whether the "Request Deletion" button should be shown.
    /// </summary>
    /// <param name="jellyfinUsername">The Jellyfin username of the current user.</param>
    /// <param name="mediaType">"movie" or "tv".</param>
    /// <param name="tmdbId">The TMDB ID of the media item.</param>
    /// <param name="mediaTitle">The display title of the media item.</param>
    /// <returns>Media request status for this user and media item.</returns>
    public async Task<MediaRequestStatusDto> GetMediaRequestStatusAsync(
        string jellyfinUsername,
        string mediaType,
        int tmdbId,
        string mediaTitle)
    {
        var notFound = new MediaRequestStatusDto
        {
            MediaType = mediaType,
            MediaTitle = mediaTitle,
        };

        var config = Plugin.Instance?.Configuration;
        if (config is null
            || string.IsNullOrWhiteSpace(config.JellyseerrUrl)
            || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            return notFound;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("JellySeerr");
            client.DefaultRequestHeaders.Add(ApiKeyHeader, config.JellyseerrApiKey);

            var url = $"{config.JellyseerrUrl.TrimEnd('/')}/api/v1/{mediaType}/{tmdbId}";
            _logger.LogDebug(
                "JellySeerr Integration: checking requests for {MediaType}/{TmdbId} at {Url}",
                mediaType,
                tmdbId,
                url);

            var detail = await client.GetFromJsonAsync<JellyseerrMediaDetailResponse>(url).ConfigureAwait(false);

            var requests = detail?.MediaInfo?.Requests;
            if (requests is null || requests.Count == 0)
            {
                return notFound;
            }

            var matchedRequest = requests.Find(r =>
                string.Equals(
                    r.RequestedBy?.JellyfinUsername,
                    jellyfinUsername,
                    StringComparison.OrdinalIgnoreCase));

            if (matchedRequest is null)
            {
                return notFound;
            }

            return new MediaRequestStatusDto
            {
                HasRequest = true,
                MediaType = mediaType,
                MediaTitle = mediaTitle,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "JellySeerr Integration: failed to fetch media request status for {MediaType}/{TmdbId}",
                mediaType,
                tmdbId);
            return notFound;
        }
    }

    /// <summary>
    /// Submits a deletion request to the configured endpoint on behalf of the current user.
    /// </summary>
    /// <param name="jellyfinToken">The user's Jellyfin access token, used as the Bearer token.</param>
    /// <param name="jellyfinMediaId">The native Jellyfin item ID (GUID string).</param>
    /// <param name="mediaTitle">The human-readable title of the media item.</param>
    /// <param name="mediaType">The media type: "movie" or "tv".</param>
    /// <returns>A <see cref="DeletionRequestResult"/> indicating the outcome.</returns>
    public async Task<DeletionRequestResult> SendDeletionRequestAsync(
        string jellyfinToken,
        string jellyfinMediaId,
        string mediaTitle,
        string mediaType)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.DeleteRequestWebhookUrl))
        {
            _logger.LogWarning("JellySeerr Integration: delete request endpoint URL is not configured");
            return DeletionRequestResult.Failure;
        }

        var payload = new
        {
            jellyfin_media_id = jellyfinMediaId,
            media_title = mediaTitle,
            media_type = mediaType,
        };

        try
        {
            using var client = _httpClientFactory.CreateClient("JellySeerr");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, config.DeleteRequestWebhookUrl);
            requestMessage.Headers.Add("Authorization", $"Bearer {jellyfinToken}");
            requestMessage.Content = JsonContent.Create(payload);

            var response = await client.SendAsync(requestMessage).ConfigureAwait(false);

            if ((int)response.StatusCode == 409)
            {
                _logger.LogInformation(
                    "JellySeerr Integration: deletion request for '{MediaId}' already pending",
                    jellyfinMediaId);
                return DeletionRequestResult.Conflict;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning(
                    "JellySeerr Integration: delete request endpoint returned {StatusCode} for item '{MediaId}': {Body}",
                    (int)response.StatusCode,
                    jellyfinMediaId,
                    body);
                return DeletionRequestResult.Failure;
            }

            _logger.LogInformation(
                "JellySeerr Integration: deletion request submitted successfully for item '{MediaId}' — '{Title}'",
                jellyfinMediaId,
                mediaTitle);
            return DeletionRequestResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "JellySeerr Integration: exception while submitting deletion request for item '{MediaId}'",
                jellyfinMediaId);
            return DeletionRequestResult.Failure;
        }
    }

    /// <summary>
    /// Searches JellySeerr for movies and TV shows matching the given query.
    /// Person results and already-available items (status 5) are excluded.
    /// Returns an empty list on any error.
    /// </summary>
    /// <param name="query">The search query string.</param>
    /// <returns>List of simplified search result DTOs.</returns>
    public async Task<List<JellyseerrSearchResultDto>> SearchMediaAsync(string query)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null
            || string.IsNullOrWhiteSpace(config.JellyseerrUrl)
            || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            return new List<JellyseerrSearchResultDto>();
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("JellySeerr");
            client.DefaultRequestHeaders.Add(ApiKeyHeader, config.JellyseerrApiKey);

            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"{config.JellyseerrUrl.TrimEnd('/')}/api/v1/search?query={encodedQuery}";
            _logger.LogDebug("JellySeerr Integration: searching for '{Query}' at {Url}", query, url);

            var response = await client.GetFromJsonAsync<JellyseerrRawSearchResponse>(url).ConfigureAwait(false);

            if (response?.Results is null)
            {
                return new List<JellyseerrSearchResultDto>();
            }

            var results = new List<JellyseerrSearchResultDto>(response.Results.Count);
            foreach (var r in response.Results)
            {
                if (string.Equals(r.MediaType, "person", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var status = r.MediaInfo?.Status ?? 0;
                if (status != 0)
                {
                    continue;
                }

                results.Add(new JellyseerrSearchResultDto
                {
                    Id = r.Id,
                    MediaType = r.MediaType ?? string.Empty,
                    Title = r.Title,
                    Name = r.Name,
                    PosterPath = r.PosterPath,
                    ReleaseDate = r.ReleaseDate,
                    FirstAirDate = r.FirstAirDate,
                    MediaStatus = status,
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JellySeerr Integration: failed to search for '{Query}'", query);
            return new List<JellyseerrSearchResultDto>();
        }
    }

    /// <summary>
    /// Fetches the available season numbers for a TV show from JellySeerr using the admin key.
    /// </summary>
    /// <param name="baseUrl">The JellySeerr base URL (trailing slash will be trimmed).</param>
    /// <param name="tmdbId">The TMDB ID of the TV show.</param>
    /// <returns>Array of season numbers greater than 0, or null on failure.</returns>
    public async Task<int[]?> GetTvSeasonsAsync(string baseUrl, int tmdbId)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            return null;
        }

        using var client = _httpClientFactory.CreateClient("JellySeerr");
        client.DefaultRequestHeaders.Add(ApiKeyHeader, config.JellyseerrApiKey);
        return await FetchTvSeasonsAsync(client, baseUrl.TrimEnd('/'), tmdbId).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the cached user record for the given Jellyfin username, forcing a fresh lookup.
    /// </summary>
    /// <param name="jellyfinUsername">The Jellyfin username whose cache entry to remove.</param>
    public void InvalidateCache(string jellyfinUsername) => _cache.Remove(jellyfinUsername);

    private async Task<int[]?> FetchTvSeasonsAsync(HttpClient client, string baseUrl, int tvId)
    {
        try
        {
            var url = $"{baseUrl}/api/v1/tv/{tvId}";
            var detail = await client.GetFromJsonAsync<JellyseerrTvDetailResponse>(url).ConfigureAwait(false);
            if (detail?.Seasons is null || detail.Seasons.Count == 0)
            {
                return null;
            }

            var seasonNumbers = detail.Seasons
                .Where(s => s.SeasonNumber > 0)
                .Select(s => s.SeasonNumber)
                .ToArray();

            return seasonNumbers.Length > 0 ? seasonNumbers : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JellySeerr Integration: failed to fetch season list for TV/{TvId}", tvId);
            return null;
        }
    }

    private async Task<JellyseerrUser?> FindUserAsync(string jellyfinUsername)
    {
        if (_cache.TryGetValue(jellyfinUsername, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
        {
            return cached.User;
        }

        var user = await FetchUserAsync(jellyfinUsername).ConfigureAwait(false);
        if (user is not null)
        {
            _cache[jellyfinUsername] = (DateTimeOffset.UtcNow.Add(CacheTtl), user);
        }

        return user;
    }

    private async Task<JellyseerrUser?> FetchUserAsync(string jellyfinUsername)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null
            || string.IsNullOrWhiteSpace(config.JellyseerrUrl)
            || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            return null;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("JellySeerr");
            client.DefaultRequestHeaders.Add(ApiKeyHeader, config.JellyseerrApiKey);

            var url = $"{config.JellyseerrUrl.TrimEnd('/')}/api/v1/user?take=100";
            _logger.LogDebug("JellySeerr Integration: fetching users from {Url}", url);

            var response = await client.GetFromJsonAsync<JellyseerrUsersResponse>(url).ConfigureAwait(false);

            if (response?.Results is null)
            {
                _logger.LogWarning("JellySeerr Integration: received null or empty user list from {Url}", url);
                return null;
            }

            var match = response.Results.Find(u =>
                string.Equals(u.JellyfinUsername, jellyfinUsername, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                var knownNames = string.Join(", ", response.Results.Select(u => u.JellyfinUsername ?? "(null)"));
                _logger.LogWarning(
                    "JellySeerr Integration: Jellyfin user '{Username}' not matched in {Count} Jellyseerr result(s). jellyfinUsername values: [{Known}]",
                    jellyfinUsername,
                    response.Results.Count,
                    knownNames);
            }

            return match;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "JellySeerr Integration: failed to fetch user list from JellySeerr for '{Username}'",
                jellyfinUsername);
            return null;
        }
    }
}
