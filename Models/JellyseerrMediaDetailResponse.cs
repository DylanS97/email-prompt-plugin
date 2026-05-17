using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Minimal parse of the JellySeerr /api/v1/movie/{id} and /api/v1/tv/{id} responses.
/// Only the fields needed to determine whether the current user has a request are mapped.
/// </summary>
public sealed class JellyseerrMediaDetailResponse
{
    /// <summary>
    /// Gets or sets the media info block which contains request history.
    /// </summary>
    [JsonPropertyName("mediaInfo")]
    public JellyseerrMediaInfo? MediaInfo { get; set; }
}
