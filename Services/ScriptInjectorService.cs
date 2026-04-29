using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyseerrIntegration.Services;

/// <summary>
/// Injects the plugin client script into Jellyfin's index.html at server startup.
/// </summary>
public class ScriptInjectorService : IHostedService
{
    private const string ScriptTag = "<script src=\"/JellyseerrIntegration/Script\"></script>";

    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<ScriptInjectorService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptInjectorService"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ScriptInjectorService}"/> interface.</param>
    public ScriptInjectorService(IApplicationPaths applicationPaths, ILogger<ScriptInjectorService> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var indexPath = Path.Combine(_applicationPaths.WebPath, "index.html");
        if (!File.Exists(indexPath))
        {
            _logger.LogWarning("JellySeerr Integration: index.html not found at {Path}, skipping script injection", indexPath);
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(indexPath, cancellationToken).ConfigureAwait(false);

            if (content.Contains(ScriptTag, StringComparison.Ordinal))
            {
                _logger.LogDebug("JellySeerr Integration: script already present in index.html");
                return;
            }

            var modified = content.Replace("</body>", ScriptTag + "\n</body>", StringComparison.OrdinalIgnoreCase);
            await File.WriteAllTextAsync(indexPath, modified, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("JellySeerr Integration: injected script into index.html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JellySeerr Integration: failed to inject script into index.html");
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var indexPath = Path.Combine(_applicationPaths.WebPath, "index.html");
        if (!File.Exists(indexPath))
        {
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(indexPath, cancellationToken).ConfigureAwait(false);

            if (!content.Contains(ScriptTag, StringComparison.Ordinal))
            {
                return;
            }

            var modified = content.Replace(ScriptTag + "\n", string.Empty, StringComparison.Ordinal);
            await File.WriteAllTextAsync(indexPath, modified, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("JellySeerr Integration: removed script from index.html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JellySeerr Integration: failed to remove script from index.html");
        }
    }
}
