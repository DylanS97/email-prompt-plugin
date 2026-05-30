using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Internal deserialization model for a single result from GET /api/v1/search.
/// </summary>
internal sealed class JellyseerrRawSearchResult
{
    /// <summary>Gets or sets the TMDB ID.</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Gets or sets the media type string (e.g. "movie", "tv", "person").</summary>
    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }

    /// <summary>Gets or sets the movie title (movies only).</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Gets or sets the show name (TV shows only).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the TMDB poster path.</summary>
    [JsonPropertyName("posterPath")]
    public string? PosterPath { get; set; }

    /// <summary>Gets or sets the release date string (movies only).</summary>
    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }

    /// <summary>Gets or sets the first air date string (TV shows only).</summary>
    [JsonPropertyName("firstAirDate")]
    public string? FirstAirDate { get; set; }

    /// <summary>Gets or sets the JellySeerr media info block.</summary>
    [JsonPropertyName("mediaInfo")]
    public JellyseerrRawMediaInfo? MediaInfo { get; set; }
}
