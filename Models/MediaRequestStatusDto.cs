namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Response DTO for the media request status check endpoint.
/// </summary>
public sealed class MediaRequestStatusDto
{
    /// <summary>
    /// Gets or sets a value indicating whether the current user has a JellySeerr request for this media.
    /// </summary>
    public bool HasRequest { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the delete button feature is enabled and the endpoint URL is configured.
    /// </summary>
    public bool WebhookConfigured { get; set; }

    /// <summary>
    /// Gets or sets the media type ("movie" or "tv"), echoed back for use in the deletion request.
    /// </summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media title, echoed back for use in the deletion request.
    /// </summary>
    public string MediaTitle { get; set; } = string.Empty;
}
