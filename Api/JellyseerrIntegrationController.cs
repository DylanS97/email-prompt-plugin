using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyseerrIntegration.Models;
using Jellyfin.Plugin.JellyseerrIntegration.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyseerrIntegration.Api;

/// <summary>
/// API endpoints for the JellySeerr Integration plugin.
/// </summary>
[ApiController]
[Route("JellyseerrIntegration")]
public class JellyseerrIntegrationController : ControllerBase
{
    private readonly IAuthorizationContext _authContext;
    private readonly JellyseerrService _jellyseerrService;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<JellyseerrIntegrationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyseerrIntegrationController"/> class.
    /// </summary>
    /// <param name="authContext">Instance of the <see cref="IAuthorizationContext"/> interface.</param>
    /// <param name="jellyseerrService">Instance of the <see cref="JellyseerrService"/>.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{JellyseerrIntegrationController}"/> interface.</param>
    public JellyseerrIntegrationController(
        IAuthorizationContext authContext,
        JellyseerrService jellyseerrService,
        ILibraryManager libraryManager,
        ILogger<JellyseerrIntegrationController> logger)
    {
        _authContext = authContext;
        _jellyseerrService = jellyseerrService;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Returns whether the currently authenticated Jellyfin user should be prompted to add their
    /// email to their JellySeerr account.
    /// </summary>
    /// <returns>Email prompt status for the current user.</returns>
    [HttpGet("EmailPrompt/Status")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EmailPromptStatusDto>> GetEmailPromptStatus()
    {
        var authInfo = await _authContext.GetAuthorizationInfo(Request).ConfigureAwait(false);
        var user = authInfo.User;

        if (user is null)
        {
            _logger.LogWarning("JellySeerr Integration: GetEmailPromptStatus called with no authenticated user");
            return Unauthorized();
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null
            || !config.EnableEmailPrompt
            || string.IsNullOrWhiteSpace(config.JellyseerrUrl)
            || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            _logger.LogDebug("JellySeerr Integration: email prompt is disabled or plugin is not configured");
            return Ok(new EmailPromptStatusDto { NeedsEmail = false });
        }

        var status = await _jellyseerrService.GetUserEmailStatusAsync(user.Username).ConfigureAwait(false);
        return Ok(status);
    }

    /// <summary>
    /// Updates the email on the current user's JellySeerr account.
    /// </summary>
    /// <param name="request">The email update request.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("EmailPrompt/Email")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request?.Email) || !request.Email.Contains('@'))
        {
            _logger.LogWarning("JellySeerr Integration: UpdateEmail called with invalid email '{Email}'", request?.Email);
            return BadRequest("A valid email address is required.");
        }

        var authInfo = await _authContext.GetAuthorizationInfo(Request).ConfigureAwait(false);
        var user = authInfo.User;

        if (user is null)
        {
            _logger.LogWarning("JellySeerr Integration: UpdateEmail called with no authenticated user");
            return Unauthorized();
        }

        _logger.LogInformation(
            "JellySeerr Integration: '{Username}' is requesting email update",
            user.Username);

        var success = await _jellyseerrService.UpdateUserEmailAsync(user.Username, request.Email).ConfigureAwait(false);

        if (!success)
        {
            _logger.LogWarning(
                "JellySeerr Integration: email update failed for Jellyfin user '{Username}'",
                user.Username);
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        return NoContent();
    }

    /// <summary>
    /// Returns whether the current user has a JellySeerr request for the given Jellyfin library item,
    /// and whether the "Request Deletion" button should be shown.
    /// </summary>
    /// <param name="jellyfinItemId">The Jellyfin item ID (GUID) of the media to check.</param>
    /// <returns>Media request status for the current user and item.</returns>
    [HttpGet("MediaRequest/Status")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MediaRequestStatusDto>> GetMediaRequestStatus([FromQuery] Guid jellyfinItemId)
    {
        var authInfo = await _authContext.GetAuthorizationInfo(Request).ConfigureAwait(false);
        var user = authInfo.User;

        if (user is null)
        {
            return Unauthorized();
        }

        var config = Plugin.Instance?.Configuration;
        var notFound = new MediaRequestStatusDto();

        if (config is null
            || string.IsNullOrWhiteSpace(config.JellyseerrUrl)
            || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            return Ok(notFound);
        }

        var item = _libraryManager.GetItemById(jellyfinItemId);
        if (item is null)
        {
            return Ok(notFound);
        }

        string mediaType;
        if (item is Movie)
        {
            mediaType = "movie";
        }
        else if (item is Series)
        {
            mediaType = "tv";
        }
        else
        {
            return Ok(notFound);
        }

        if (!item.ProviderIds.TryGetValue("Tmdb", out var tmdbIdStr)
            || !int.TryParse(tmdbIdStr, out var tmdbId))
        {
            _logger.LogDebug(
                "JellySeerr Integration: item '{ItemId}' has no TMDB ID — skipping media request check",
                jellyfinItemId);
            return Ok(notFound);
        }

        var status = await _jellyseerrService
            .GetMediaRequestStatusAsync(user.Username, mediaType, tmdbId, item.Name)
            .ConfigureAwait(false);

        status.WebhookConfigured = config.EnableDeleteButton
            && !string.IsNullOrWhiteSpace(config.DeleteRequestWebhookUrl);

        return Ok(status);
    }

    /// <summary>
    /// Submits a deletion request to the configured endpoint on behalf of the current user.
    /// </summary>
    /// <param name="request">The deletion request body.</param>
    /// <returns>204 No Content on success, 409 if a request already exists, 502 on failure.</returns>
    [HttpPost("DeletionRequest")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SendDeletionRequest([FromBody] DeletionRequestDto request)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.JellyfinMediaId)
            || string.IsNullOrWhiteSpace(request.MediaTitle)
            || (request.MediaType != "movie" && request.MediaType != "tv"))
        {
            return BadRequest("JellyfinMediaId, MediaTitle, and MediaType (\"movie\" or \"tv\") are required.");
        }

        var authInfo = await _authContext.GetAuthorizationInfo(Request).ConfigureAwait(false);
        var user = authInfo.User;

        if (user is null || string.IsNullOrWhiteSpace(authInfo.Token))
        {
            return Unauthorized();
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.EnableDeleteButton || string.IsNullOrWhiteSpace(config.DeleteRequestWebhookUrl))
        {
            _logger.LogWarning("JellySeerr Integration: delete request endpoint is not configured or disabled");
            return BadRequest("Delete request endpoint is not configured.");
        }

        _logger.LogInformation(
            "JellySeerr Integration: '{Username}' is submitting a deletion request for '{Title}' (item: {ItemId})",
            user.Username,
            request.MediaTitle,
            request.JellyfinMediaId);

        var result = await _jellyseerrService
            .SendDeletionRequestAsync(authInfo.Token, request.JellyfinMediaId, request.MediaTitle, request.MediaType)
            .ConfigureAwait(false);

        return result switch
        {
            Models.DeletionRequestResult.Success => NoContent(),
            Models.DeletionRequestResult.Conflict => Conflict(new { message = "You already have a pending deletion request for this item." }),
            _ => StatusCode(StatusCodes.Status502BadGateway),
        };
    }

    /// <summary>
    /// Searches JellySeerr for movies and TV shows matching the given query.
    /// Person results and already-available items are excluded.
    /// </summary>
    /// <param name="query">The search term.</param>
    /// <returns>List of requestable JellySeerr search results.</returns>
    [HttpGet("Search")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<JellyseerrSearchResultDto>>> SearchMedia([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("query is required.");
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null
            || !config.EnableSearchIntegration
            || string.IsNullOrWhiteSpace(config.JellyseerrUrl)
            || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            return Ok(Array.Empty<JellyseerrSearchResultDto>());
        }

        var results = await _jellyseerrService.SearchMediaAsync(query).ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Returns the JellySeerr base URL and (for TV shows) the available season numbers so
    /// the browser can submit the request directly to JellySeerr using the user's own session.
    /// </summary>
    /// <param name="mediaType">The media type: "movie" or "tv".</param>
    /// <param name="mediaId">The TMDB ID of the media to request.</param>
    /// <returns>JellySeerr URL and season list for TV shows.</returns>
    [HttpGet("MediaRequest/Prep")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RequestPrepDto>> GetRequestPrep(
        [FromQuery] string mediaType,
        [FromQuery] int mediaId)
    {
        if ((mediaType != "movie" && mediaType != "tv") || mediaId <= 0)
        {
            return BadRequest("mediaType (\"movie\" or \"tv\") and a positive mediaId are required.");
        }

        var authInfo = await _authContext.GetAuthorizationInfo(Request).ConfigureAwait(false);
        if (authInfo.User is null)
        {
            return Unauthorized();
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null
            || !config.EnableSearchIntegration
            || string.IsNullOrWhiteSpace(config.JellyseerrUrl)
            || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        int[]? seasons = null;
        if (string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            seasons = await _jellyseerrService
                .GetTvSeasonsAsync(config.JellyseerrUrl, mediaId)
                .ConfigureAwait(false);
        }

        return Ok(new RequestPrepDto
        {
            JellyseerrUrl = config.JellyseerrUrl.TrimEnd('/'),
            Seasons = seasons,
        });
    }

    /// <summary>
    /// Serves the plugin client-side JavaScript file.
    /// </summary>
    /// <returns>The plugin JavaScript.</returns>
    [HttpGet("Script")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetScript()
    {
        const string ResourceName = "Jellyfin.Plugin.JellyseerrIntegration.Web.jellyseerr.js";
        var stream = typeof(Plugin).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            _logger.LogError("JellySeerr Integration: embedded resource '{Resource}' not found", ResourceName);
            return NotFound();
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Content(reader.ReadToEnd(), "application/javascript");
    }
}
