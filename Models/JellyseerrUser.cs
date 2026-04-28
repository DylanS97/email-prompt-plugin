using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// JellySeerr user object returned by the /api/v1/users endpoint.
/// </summary>
public sealed class JellyseerrUser
{
    /// <summary>
    /// Gets or sets the JellySeerr internal user ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the account email. Empty string when not set.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the linked Jellyfin username.
    /// </summary>
    [JsonPropertyName("jellyfinUsername")]
    public string? JellyfinUsername { get; set; }
}
