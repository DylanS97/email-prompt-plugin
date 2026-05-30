namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Request body sent by the client when a user clicks the "Request Deletion" button.
/// </summary>
public sealed class DeletionRequestDto
{
    /// <summary>
    /// Gets or sets the native Jellyfin item ID (GUID string) for the media item.
    /// </summary>
    public string JellyfinMediaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media type: must be exactly "movie" or "tv".
    /// </summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable title of the media item.
    /// </summary>
    public string MediaTitle { get; set; } = string.Empty;
}
