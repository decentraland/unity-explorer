using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public interface IDCLGlobalPluginWithoutSettings : IDCLGlobalPlugin<NoExposedPluginSettings>
    {
        UniTask IDCLPlugin.Initialize(IPluginSettingsContainer container, CancellationToken ct) =>

            // Don't even try to retrieve empty settings
            UniTask.CompletedTask;

        UniTask IDCLPlugin<NoExposedPluginSettings>.Initialize(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        void IDisposable.Dispose() { }
    }
}
