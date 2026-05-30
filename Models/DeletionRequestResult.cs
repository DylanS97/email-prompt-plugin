namespace Jellyfin.Plugin.JellyseerrIntegration.Models;

/// <summary>
/// Result of a deletion request submission to the configured endpoint.
/// </summary>
public enum DeletionRequestResult
{
    /// <summary>Request was accepted (2xx).</summary>
    Success,

    /// <summary>User already has a pending or approved deletion request for this item (409).</summary>
    Conflict,

    /// <summary>The request failed for any other reason.</summary>
    Failure,
}
