namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// A single JellySeerr search result returned to the client.
/// </summary>
public sealed class JellyseerrSearchResultDto
{
    /// <summary>Gets or sets the TMDB ID of this media item.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the media type: "movie" or "tv".</summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>Gets or sets the movie title (movies only).</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the show name (TV only).</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the TMDB poster path (relative, e.g. "/abc.jpg").</summary>
    public string? PosterPath { get; set; }

    /// <summary>Gets or sets the release date string (movies only).</summary>
    public string? ReleaseDate { get; set; }

    /// <summary>Gets or sets the first air date string (TV only).</summary>
    public string? FirstAirDate { get; set; }

    /// <summary>
    /// Gets or sets the JellySeerr media status: 0=none, 2=pending, 3=processing, 4=partial, 5=available.
    /// </summary>
    public int MediaStatus { get; set; }
}
