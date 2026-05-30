using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Response returned by the MediaRequest/Prep endpoint; contains the JellySeerr base URL
/// and (for TV shows) the season numbers so the browser can submit the request directly.
/// </summary>
public sealed class RequestPrepDto
{
    /// <summary>Gets or sets the base URL of the JellySeerr instance.</summary>
    [JsonPropertyName("jellyseerrUrl")]
    public string JellyseerrUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the season numbers to include in the request (TV only; null for movies).</summary>
    [JsonPropertyName("seasons")]
    public int[]? Seasons { get; set; }
}
