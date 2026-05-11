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
    /// Removes the cached user record for the given Jellyfin username, forcing a fresh lookup.
    /// </summary>
    /// <param name="jellyfinUsername">The Jellyfin username whose cache entry to remove.</param>
    public void InvalidateCache(string jellyfinUsername) => _cache.Remove(jellyfinUsername);

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
