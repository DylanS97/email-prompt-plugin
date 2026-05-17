using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// A single request entry within JellySeerr media info.
/// </summary>
public sealed class JellyseerrMediaRequest
{
    /// <summary>
    /// Gets or sets the user who submitted this request.
    /// </summary>
    [JsonPropertyName("requestedBy")]
    public JellyseerrUser? RequestedBy { get; set; }
}
