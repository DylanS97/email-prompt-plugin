using Jellyfin.Plugin.JellyseerrIntegration.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.JellyseerrIntegration;

/// <summary>
/// Registers plugin services with the dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient("JellySeerr");
        serviceCollection.AddSingleton<JellyseerrService>();
        serviceCollection.AddHostedService<ScriptInjectorService>();
    }
}
