using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyseerrIntegration.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the email prompt feature is enabled.
    /// </summary>
    public bool EnableEmailPrompt { get; set; } = true;

    /// <summary>
    /// Gets or sets the JellySeerr base URL (no trailing slash), e.g. http://jellyseerr:5055.
    /// </summary>
    public string JellyseerrUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JellySeerr admin API key (from JellySeerr Settings → General).
    /// </summary>
    public string JellyseerrApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional override URL shown in the banner instead of the direct JellySeerr link.
    /// Supports the {username} placeholder to include the user's Jellyfin username.
    /// </summary>
    public string CustomFormUrl { get; set; } = string.Empty;
}
