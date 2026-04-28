using System;
using System.Collections.Generic;
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
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JellyseerrService> _logger;
    private readonly Dictionary<string, (DateTimeOffset Expires, EmailPromptStatusDto Result)> _cache
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
    /// Returns NeedsEmail=false (no banner) if the user cannot be found in JellySeerr or on any error.
    /// </summary>
    /// <param name="jellyfinUsername">The Jellyfin username to look up.</param>
    /// <returns>Email prompt status for this user.</returns>
    public async Task<EmailPromptStatusDto> GetUserEmailStatusAsync(string jellyfinUsername)
    {
        if (_cache.TryGetValue(jellyfinUsername, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
        {
            return cached.Result;
        }

        var result = await FetchStatusAsync(jellyfinUsername).ConfigureAwait(false);
        _cache[jellyfinUsername] = (DateTimeOffset.UtcNow.Add(CacheTtl), result);
        return result;
    }

    /// <summary>
    /// Removes the cached status for a user, forcing a fresh lookup on the next request.
    /// </summary>
    /// <param name="jellyfinUsername">The Jellyfin username whose cache entry to remove.</param>
    public void InvalidateCache(string jellyfinUsername) => _cache.Remove(jellyfinUsername);

    private async Task<EmailPromptStatusDto> FetchStatusAsync(string jellyfinUsername)
    {
        var noPrompt = new EmailPromptStatusDto { NeedsEmail = false };

        var config = Plugin.Instance?.Configuration;
        if (config is null
            || string.IsNullOrWhiteSpace(config.JellyseerrUrl)
            || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            return noPrompt;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("JellySeerr");
            client.DefaultRequestHeaders.Add(ApiKeyHeader, config.JellyseerrApiKey);

            var url = $"{config.JellyseerrUrl.TrimEnd('/')}/api/v1/users?take=100";
            var response = await client.GetFromJsonAsync<JellyseerrUsersResponse>(url).ConfigureAwait(false);

            if (response?.Results is null)
            {
                return noPrompt;
            }

            var match = response.Results.Find(u =>
                string.Equals(u.JellyfinUsername, jellyfinUsername, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                _logger.LogDebug(
                    "JellySeerr Integration: no JellySeerr user found for Jellyfin username '{Username}'",
                    jellyfinUsername);
                return noPrompt;
            }

            if (!string.IsNullOrWhiteSpace(match.Email))
            {
                return noPrompt;
            }

            string settingsUrl;
            if (!string.IsNullOrWhiteSpace(config.CustomFormUrl))
            {
                settingsUrl = config.CustomFormUrl.Replace(
                    "{username}",
                    jellyfinUsername,
                    StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                settingsUrl = $"{config.JellyseerrUrl.TrimEnd('/')}/users/{match.Id}/settings/notifications";
            }

            return new EmailPromptStatusDto { NeedsEmail = true, SettingsUrl = settingsUrl };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JellySeerr Integration: failed to fetch user status from JellySeerr");
            return noPrompt;
        }
    }
}
