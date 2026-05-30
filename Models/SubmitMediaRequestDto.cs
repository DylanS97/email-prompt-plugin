namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Request body sent by the client when submitting a JellySeerr media request.
/// </summary>
public sealed class SubmitMediaRequestDto
{
    /// <summary>Gets or sets the media type: "movie" or "tv".</summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>Gets or sets the TMDB ID of the media to request.</summary>
    public int MediaId { get; set; }
}
