using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Global.Dynamic;
using System;
using System.Threading;
using Utility;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Owns the embedded abgen JIT server for local scene development with local asset bundles:
    ///     launches the port-reserved (not yet started) sidecar, kicks the eager whole-scene warm-up once
    ///     it is healthy, and kills the child process on dispose. Registered only when MainSceneLoader
    ///     reserved a sidecar (local scene development + local-ab, no explicit --optimized-assets-url) —
    ///     in every other mode this plugin is never constructed.
    /// </summary>
    public class AbgenSidecarPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly AbgenSidecar sidecar;
        private CancellationTokenSource? lifeCycleCancellationTokenSource;

        public AbgenSidecarPlugin(AbgenSidecar sidecar)
        {
            this.sidecar = sidecar;
        }

        public void Dispose()
        {
            lifeCycleCancellationTokenSource.SafeCancelAndDispose();
            sidecar.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            lifeCycleCancellationTokenSource = lifeCycleCancellationTokenSource.SafeRestart();
            StartAndWarmUpAsync(lifeCycleCancellationTokenSource.Token).Forget();
        }

        private async UniTaskVoid StartAndWarmUpAsync(CancellationToken ct)
        {
            try
            {
                if (await sidecar.StartAsync(ct))
                    sidecar.WarmUpLocalSceneAsync(ct).Forget();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES); }
        }
    }
}
