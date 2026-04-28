using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Paginated response from GET /api/v1/users.
/// </summary>
public sealed class JellyseerrUsersResponse
{
    /// <summary>
    /// Gets or sets the list of users on this page.
    /// </summary>
    [JsonPropertyName("results")]
    public List<JellyseerrUser> Results { get; set; } = [];
}
