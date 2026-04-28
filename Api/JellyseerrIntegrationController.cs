using System.IO;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyseerrIntegration.Models;
using Jellyfin.Plugin.JellyseerrIntegration.Services;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyseerrIntegrationController"/> class.
    /// </summary>
    /// <param name="authContext">Instance of the <see cref="IAuthorizationContext"/> interface.</param>
    /// <param name="jellyseerrService">Instance of the <see cref="JellyseerrService"/>.</param>
    public JellyseerrIntegrationController(
        IAuthorizationContext authContext,
        JellyseerrService jellyseerrService)
    {
        _authContext = authContext;
        _jellyseerrService = jellyseerrService;
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
            return Unauthorized();
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null
            || !config.EnableEmailPrompt
            || string.IsNullOrWhiteSpace(config.JellyseerrUrl)
            || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            return Ok(new EmailPromptStatusDto { NeedsEmail = false });
        }

        var status = await _jellyseerrService.GetUserEmailStatusAsync(user.Username).ConfigureAwait(false);
        return Ok(status);
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
            return NotFound();
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Content(reader.ReadToEnd(), "application/javascript");
    }
}
