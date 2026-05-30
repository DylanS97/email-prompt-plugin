using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Minimal parse of GET /api/v1/tv/{id} used to extract season numbers.
/// </summary>
internal sealed class JellyseerrTvDetailResponse
{
    /// <summary>Gets or sets the list of seasons for this TV show.</summary>
    [JsonPropertyName("seasons")]
    public List<JellyseerrTvSeason>? Seasons { get; set; }
}
