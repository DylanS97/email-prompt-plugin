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
    /// Gets or sets a value indicating whether the user has an email address set in JellySeerr.
    /// The deletion button is only shown when both HasRequest and HasEmail are true.
    /// </summary>
    public bool HasEmail { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the admin has configured a deletion webhook URL.
    /// </summary>
    public bool WebhookConfigured { get; set; }

    /// <summary>
    /// Gets or sets the JellySeerr media type ("movie" or "tv"), echoed back so the client
    /// can include it in the deletion webhook request without re-parsing.
    /// </summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TMDB ID, echoed back for use in the deletion webhook request.
    /// </summary>
    public int TmdbId { get; set; }

    /// <summary>
    /// Gets or sets the media title, used to populate the webhook subject/message.
    /// </summary>
    public string MediaTitle { get; set; } = string.Empty;
}
