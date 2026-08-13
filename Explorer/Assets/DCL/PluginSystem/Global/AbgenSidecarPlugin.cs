using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.StreamableLoading.AssetBundles;
using Global.Dynamic;
using Global.Dynamic.RealmUrl;
using System;
using System.Threading;
using Utility;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Owns the embedded abgen JIT server for local scene development with local asset bundles:
    ///     resolves the realm, downloads the pinned binary on first run, creates the sidecar on the
    ///     pre-reserved loopback endpoint the URL sources already point at, launches it, runs the eager
    ///     whole-scene warm-up once it is healthy, and kills the child process on dispose.
    ///     <see cref="ReadyAsync" /> signals the terminal state — boot holds realm loading on it so the
    ///     scene's manifest is never requested against a server that is still warming up. Registered
    ///     only in that mode — otherwise this plugin is never constructed.
    /// </summary>
    public class AbgenSidecarPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly string baseUrl;
        private readonly RealmUrls realmUrls;
        private readonly DecentralandEnvironment environment;
        private readonly UniTaskCompletionSource readyCompletionSource = new ();

        private AbgenSidecar? sidecar;
        private CancellationTokenSource? lifeCycleCancellationTokenSource;

        /// <summary>
        ///     Completes when the sidecar reaches a terminal state: warm and serving (whole-scene warm-up
        ///     finished), or given up (no binary and the download failed, launch failure, cancellation).
        ///     Never faults. Single awaiter only.
        /// </summary>
        public UniTask ReadyAsync => readyCompletionSource.Task;

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

            // Covers teardown before InjectToWorld ever ran; otherwise RunAsync's finally completes it.
            readyCompletionSource.TrySetResult();
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
                string environmentDomain = environment.ToString().ToLower();

                AbgenSidecar? created = AbgenSidecar.TryCreate(baseUrl, environmentDomain,
                    realmRootOverride: realmRoot, jitContentDigest: true);

                if (created == null)
                {
                    // First run: no binary installed. Download it now; the AB panel open-request is
                    // consumed as soon as the debug menu is on screen, showing what ran during boot.
                    AbgenConversionMetrics.INSTANCE.RequestPanelOpen();

                    if (!await AbgenSidecar.EnsurePinnedBinaryAsync(ct))
                        return;

                    created = AbgenSidecar.TryCreate(baseUrl, environmentDomain,
                        realmRootOverride: realmRoot, jitContentDigest: true);

                    if (created == null) return;
                }

                sidecar = created;

                if (await sidecar.StartAsync(ct))
                    await sidecar.WarmUpLocalSceneAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES); }
            finally { readyCompletionSource.TrySetResult(); }
        }
    }
}
