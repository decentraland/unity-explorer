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

        void IDisposable.Dispose() { }

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings,
            CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }
    }
}
