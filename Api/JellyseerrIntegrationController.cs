using System.IO;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyseerrIntegration.Models;
using Jellyfin.Plugin.JellyseerrIntegration.Services;
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
    private readonly ILogger<JellyseerrIntegrationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyseerrIntegrationController"/> class.
    /// </summary>
    /// <param name="authContext">Instance of the <see cref="IAuthorizationContext"/> interface.</param>
    /// <param name="jellyseerrService">Instance of the <see cref="JellyseerrService"/>.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{JellyseerrIntegrationController}"/> interface.</param>
    public JellyseerrIntegrationController(
        IAuthorizationContext authContext,
        JellyseerrService jellyseerrService,
        ILogger<JellyseerrIntegrationController> logger)
    {
        _authContext = authContext;
        _jellyseerrService = jellyseerrService;
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
