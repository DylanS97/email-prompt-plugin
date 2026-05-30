using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Minimal mediaInfo block from JellySeerr search results.
/// </summary>
internal sealed class JellyseerrRawMediaInfo
{
    /// <summary>Gets or sets the JellySeerr media status code.</summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }
}
