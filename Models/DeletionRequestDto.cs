namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Request body sent by the client when a user clicks the "Request Deletion" button.
/// </summary>
public sealed class DeletionRequestDto
{
    /// <summary>
    /// Gets or sets the JellySeerr media type: "movie" or "tv".
    /// </summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TMDB ID of the media item.
    /// </summary>
    public int TmdbId { get; set; }

    /// <summary>
    /// Gets or sets the human-readable title of the media item, used in the webhook payload.
    /// </summary>
    public string MediaTitle { get; set; } = string.Empty;
}
