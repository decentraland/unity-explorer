using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using Global.Dynamic;
using Global.Dynamic.RealmUrl;
using System;
using System.Threading;
using Utility;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Owns the embedded abgen JIT server for local scene development with local asset bundles:
    ///     resolves the realm, creates the sidecar on the pre-reserved loopback endpoint the URL sources
    ///     already point at, launches it, kicks the eager whole-scene warm-up once it is healthy, and
    ///     kills the child process on dispose. Registered only in that mode — otherwise this plugin is
    ///     never constructed.
    /// </summary>
    public class AbgenSidecarPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly string baseUrl;
        private readonly RealmUrls realmUrls;
        private readonly DecentralandEnvironment environment;

        private AbgenSidecar? sidecar;
        private CancellationTokenSource? lifeCycleCancellationTokenSource;

        public AbgenSidecarPlugin(string baseUrl, RealmUrls realmUrls, DecentralandEnvironment environment)
        {
            this.baseUrl = baseUrl;
            this.realmUrls = realmUrls;
            this.environment = environment;
        }

        public void Dispose()
        {
            lifeCycleCancellationTokenSource.SafeCancelAndDispose();
            sidecar?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            lifeCycleCancellationTokenSource = lifeCycleCancellationTokenSource.SafeRestart();
            RunAsync(lifeCycleCancellationTokenSource.Token).Forget();
        }

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                // The canonical LSD realm — the same resolution the rest of the app runs on.
                string? realmRoot = await realmUrls.LocalSceneDevelopmentRealmAsync(ct);

                sidecar = AbgenSidecar.TryCreate(baseUrl, environment.ToString().ToLower(),
                    realmRootOverride: realmRoot, jitContentDigest: true);

                // null: no binary yet — the download was kicked off, the sidecar activates next launch.
                if (sidecar == null) return;

                if (await sidecar.StartAsync(ct))
                    sidecar.WarmUpLocalSceneAsync(ct).Forget();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES); }
        }
    }
}
