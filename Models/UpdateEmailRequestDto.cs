using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Request body for the email update endpoint.
/// </summary>
public sealed class UpdateEmailRequestDto
{
    /// <summary>
    /// Gets or sets the email address to set on the JellySeerr account.
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}
