using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public interface IDCLGlobalPluginWithoutSettings : IDCLGlobalPlugin<NoExposedPluginSettings>
    {
        void IDisposable.Dispose() { }

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
