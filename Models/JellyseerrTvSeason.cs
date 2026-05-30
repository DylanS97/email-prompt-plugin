using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// A single season entry from the JellySeerr TV detail response.
/// </summary>
internal sealed class JellyseerrTvSeason
{
    /// <summary>Gets or sets the season number (0 = specials).</summary>
    [JsonPropertyName("seasonNumber")]
    public int SeasonNumber { get; set; }
}
