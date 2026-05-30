using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Wrapper for the GET /api/v1/search response.
/// </summary>
internal sealed class JellyseerrRawSearchResponse
{
    /// <summary>Gets or sets the list of search results.</summary>
    [JsonPropertyName("results")]
    public List<JellyseerrRawSearchResult>? Results { get; set; }
}
