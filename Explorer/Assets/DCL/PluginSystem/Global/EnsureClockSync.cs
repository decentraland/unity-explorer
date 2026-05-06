using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Time;
using DCL.WebRequests;
using System;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class EnsureClockSync
    {
        private const double CLOCK_DESYNC_THRESHOLD_SECONDS = 60d;
        private const string CLOCK_PROBE_URL = "https://decentraland.org/";

        public delegate UniTask<Result> RequestUserActionDelegate(CancellationToken ct);

        private readonly RealmClock realmClock;
        private readonly IWebRequestController webRequestController;
        private readonly RequestUserActionDelegate requestUserAction;

        public EnsureClockSync(RealmClock realmClock,
            IWebRequestController webRequestController,
            RequestUserActionDelegate requestUserAction)
        {
            this.realmClock = realmClock;
            this.webRequestController = webRequestController;
            this.requestUserAction = requestUserAction;
        }

        public async UniTask Execute(CancellationToken ct)
        {
            Result response = Result.RESTART;

            while (response == Result.RESTART)
            {
                await TryProbeServerTimeAsync(realmClock, webRequestController, ct);

                if (!IsDesync()) return;

                response = await requestUserAction(ct);
            }
        }

        private bool IsDesync()
        {
            DateTime? serverUtc = realmClock.UtcNow;

            // Don't block the user if the server time cannot be resolved
            if (!serverUtc.HasValue) return false;

            var delta = serverUtc.Value - DateTime.UtcNow;
            return Math.Abs(delta.TotalSeconds) > CLOCK_DESYNC_THRESHOLD_SECONDS;
        }

        private static async UniTask TryProbeServerTimeAsync(RealmClock realmClock, IWebRequestController controller, CancellationToken ct)
        {
            if (realmClock.HasSample) return;

            try
            {
                // This will internally set the realm clock on success
                await controller.IsHeadReachableAsync(
                    ReportCategory.STARTUP,
                    URLAddress.FromString(CLOCK_PROBE_URL),
                    ct,
                    suppressErrors: true);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.STARTUP); }
        }

        public enum Result
        {
            CONTINUE,
            RESTART,
        }
    }
}
