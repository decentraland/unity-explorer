using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using ECS.StreamableLoading.AssetBundles;
using System;
using System.Threading;
using Utility;

namespace Global.Dynamic
{
    /// <summary>
    ///     Owns the embedded abgen JIT server for local scene development with local asset bundles:
    ///     <see cref="StartAsync" /> brings it up to health, then the whole-scene warm-up runs in the
    ///     background as <see cref="WarmUpTask" />. Every way the server can fail to come up — no release
    ///     for the platform, a failed download, a launch that never answers, a blocked port — ends in the
    ///     same verdict on <see cref="AbgenConversionMetrics" />: the scene loads raw GLTFs, and the AB
    ///     panel says why. Only a server this instance launched is killed on dispose — one adopted from
    ///     another client outlives it.
    /// </summary>
    public sealed class AbgenSidecarBootstrap : IDisposable
    {
        private readonly string baseDomain;
        private readonly CancellationTokenSource lifeCycleCancellationTokenSource = new ();

        private AbgenSidecar? sidecar;

        /// <summary>The loopback endpoint the server serves on, fixed at construction.</summary>
        public string BaseUrl { get; } = AbgenSidecar.ReserveBaseUrl();

        /// <summary>
        ///     The eager whole-scene warm-up; pre-completed if <see cref="StartAsync" /> returned false.
        ///     Never faults, and its outcome carries no signal — a healthy server JIT-converts per request.
        /// </summary>
        public UniTask WarmUpTask { get; private set; } = UniTask.CompletedTask;

        public AbgenSidecarBootstrap(string baseDomain)
        {
            this.baseDomain = baseDomain;
        }

        public void Dispose()
        {
            lifeCycleCancellationTokenSource.SafeCancelAndDispose();
            sidecar?.Dispose();
        }

        /// <summary>
        ///     Brings the server up to health: resolves the binary (downloading the pinned release on first
        ///     run), launches it on <see cref="BaseUrl" /> and polls until it answers. True once it is
        ///     serving — only then is <see cref="BaseUrl" /> valid as the optimized-assets source. False
        ///     means the session degrades to raw GLTFs, already recorded with its reason on
        ///     <see cref="AbgenConversionMetrics" />. Never faults.
        /// </summary>
        public async UniTask<bool> StartAsync(string realmRoot)
        {
            CancellationToken ct = lifeCycleCancellationTokenSource.Token;

            try
            {
                AbgenSidecar? created = AbgenSidecar.TryCreate(BaseUrl, baseDomain,
                    realmRootOverride: realmRoot, jitContentDigest: true);

                if (created == null)
                {
                    // First run: download the pinned binary; the AB panel is asked to open so the wait is visible.
                    AbgenConversionMetrics.INSTANCE.RequestPanelOpen();

                    if (!await AbgenSidecar.EnsurePinnedBinaryAsync(ct))
                        return DegradeToRawGltfs("the abgen binary could not be installed");

                    created = AbgenSidecar.TryCreate(BaseUrl, baseDomain,
                        realmRootOverride: realmRoot, jitContentDigest: true);

                    if (created == null)
                        return DegradeToRawGltfs("the installed abgen binary was not found");
                }

                sidecar = created;

                if (!await sidecar.StartAsync(ct))
                    return DegradeToRawGltfs("the abgen server did not come up");
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES);
                return DegradeToRawGltfs($"the abgen sidecar failed ({e.Message})");
            }

            WarmUpTask = WarmUpAsync(sidecar, ct);
            return true;
        }

        /// <summary>
        ///     Records that the scene loads raw GLTFs and why. Always false, so a failure path is
        ///     <c>return DegradeToRawGltfs(...)</c> and nothing else.
        /// </summary>
        private static bool DegradeToRawGltfs(string reason)
        {
            ReportHub.LogWarning(ReportCategory.ASSET_BUNDLES, $"local asset bundles unavailable — the scene loads raw GLTFs: {reason}");
            AbgenConversionMetrics.INSTANCE.OnRawGltfFallback(reason);
            return false;
        }

        private static async UniTask WarmUpAsync(AbgenSidecar sidecar, CancellationToken ct)
        {
            try { await sidecar.WarmUpLocalSceneAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES); }
        }
    }
}
