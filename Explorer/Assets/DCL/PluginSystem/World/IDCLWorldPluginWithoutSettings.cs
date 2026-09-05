using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public interface IDCLWorldPluginWithoutSettings : IDCLWorldPlugin<NoExposedPluginSettings>
    {
        void IDisposable.Dispose() { }

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings,
            CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
