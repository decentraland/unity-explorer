using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.StreamableLoading.AssetBundles;
using System;
using System.Threading;
using Utility;

namespace Global.Dynamic
{
    /// <summary>
    ///     Owns the embedded abgen JIT server for local scene development with local asset bundles:
    ///     <see cref="StartAsync" /> brings it up to health, then the whole-scene warm-up runs in the
    ///     background as <see cref="WarmUpTask" />. The child process is killed on dispose.
    /// </summary>
    public sealed class AbgenSidecarBootstrap : IDisposable
    {
        private readonly DecentralandEnvironment environment;
        private readonly CancellationTokenSource lifeCycleCancellationTokenSource = new ();

        private AbgenSidecar? sidecar;

        /// <summary>The loopback endpoint the server serves on, fixed at construction.</summary>
        public string BaseUrl { get; } = AbgenSidecar.ReserveBaseUrl();

        /// <summary>
        ///     The eager whole-scene warm-up; pre-completed if <see cref="StartAsync" /> returned false.
        ///     Never faults, and its outcome carries no signal — a healthy server JIT-converts per request.
        /// </summary>
        public UniTask WarmUpTask { get; private set; } = UniTask.CompletedTask;

        public AbgenSidecarBootstrap(DecentralandEnvironment environment)
        {
            this.environment = environment;
        }

        public void Dispose()
        {
            lifeCycleCancellationTokenSource.SafeCancelAndDispose();
            sidecar?.Dispose();
        }

        /// <summary>
        ///     Brings the server up to health: resolves the binary (downloading the pinned release on first
        ///     run), launches it on <see cref="BaseUrl" /> and polls until it answers. True once it is
        ///     serving — only then is <see cref="BaseUrl" /> valid as the optimized-assets source. Never faults.
        /// </summary>
        public async UniTask<bool> StartAsync(string realmRoot)
        {
            CancellationToken ct = lifeCycleCancellationTokenSource.Token;

            try
            {
                string environmentDomain = environment.ToString().ToLower();

                AbgenSidecar? created = AbgenSidecar.TryCreate(BaseUrl, environmentDomain,
                    realmRootOverride: realmRoot, jitContentDigest: true);

                if (created == null)
                {
                    // First run: download the pinned binary; the AB panel is asked to open so the wait is visible.
                    AbgenConversionMetrics.INSTANCE.RequestPanelOpen();

                    if (!await AbgenSidecar.EnsurePinnedBinaryAsync(ct))
                        return false;

                    created = AbgenSidecar.TryCreate(BaseUrl, environmentDomain,
                        realmRootOverride: realmRoot, jitContentDigest: true);

                    if (created == null) return false;
                }

                sidecar = created;

                if (!await sidecar.StartAsync(ct))
                    return false;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES);
                return false;
            }

            WarmUpTask = WarmUpAsync(sidecar, ct);
            return true;
        }

        private static async UniTask WarmUpAsync(AbgenSidecar sidecar, CancellationToken ct)
        {
            try { await sidecar.WarmUpLocalSceneAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES); }
        }
    }
}
