using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Response returned to the Jellyfin web client for the email prompt status check.
/// </summary>
public sealed class EmailPromptStatusDto
{
    /// <summary>
    /// Gets or sets a value indicating whether the user should be prompted to add their email.
    /// </summary>
    [JsonPropertyName("needsEmail")]
    public bool NeedsEmail { get; set; }

    /// <summary>
    /// Gets or sets the URL to link to in the prompt banner.
    /// </summary>
    [JsonPropertyName("settingsUrl")]
    public string? SettingsUrl { get; set; }
}
