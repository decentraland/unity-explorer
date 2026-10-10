using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.PluginSystem
{
    public interface IDCLPlugin : IDisposable
    {
        UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            if (this is IDCLPluginWithSettings plugin)
            {
                object settings = container.GetSettings(plugin.SettingsType);
                return plugin.InitializeAsync(settings, ct);
            }

            return UniTask.CompletedTask;
        }
    }

    public interface IDCLPluginWithSettings : IDCLPlugin
    {
        Type SettingsType { get; }

        UniTask InitializeAsync(object settings, CancellationToken ct);
    }

    /// <summary>
    ///     Represents a plugin that can be injected into the ECS world
    /// </summary>
    public interface IDCLPlugin<in T> : IDCLPluginWithSettings where T: IDCLPluginSettings, new()
    {
        Type IDCLPluginWithSettings.SettingsType => typeof(T);

        UniTask IDCLPluginWithSettings.InitializeAsync(object settings, CancellationToken ct) =>
            InitializeAsync((T)settings, ct);

        UniTask InitializeAsync(T settings, CancellationToken ct);
    }
}
