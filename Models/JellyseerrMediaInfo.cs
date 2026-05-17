using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// The mediaInfo object within a JellySeerr media detail response.
/// </summary>
public sealed class JellyseerrMediaInfo
{
    /// <summary>
    /// Gets or sets the list of requests made for this media item.
    /// </summary>
    [JsonPropertyName("requests")]
    public List<JellyseerrMediaRequest>? Requests { get; set; }
}
