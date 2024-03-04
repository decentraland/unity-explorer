using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.PluginSystem
{
    public interface IDCLPlugin : IDisposable
    {
        // Add the reference to IAssetProvisioner to load from addressables
        UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct);
    }

    /// <summary>
    ///     Represents a plugin that can be injected into the ECS world
    /// </summary>
    public interface IDCLPlugin<in T> : IDCLPlugin where T: IDCLPluginSettings, new()
    {
        UniTask IDCLPlugin.Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            var settings = container.GetSettings<T>();
            return InitializeAsync(settings, ct);
        }

        UniTask InitializeAsync(T settings, CancellationToken ct);
    }
}
