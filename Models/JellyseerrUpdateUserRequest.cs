using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Request body sent to PUT /api/v1/user/{userId} on JellySeerr.
/// </summary>
public sealed class JellyseerrUpdateUserRequest
{
    /// <summary>
    /// Gets or sets the email address to set on the JellySeerr user.
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}
