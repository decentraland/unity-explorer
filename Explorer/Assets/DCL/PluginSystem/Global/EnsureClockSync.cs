using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Time;
using DCL.WebRequests;
using System;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class EnsureClockSync
    {
        private const double CLOCK_DESYNC_THRESHOLD_SECONDS = 60d;

        public delegate UniTask<Result> RequestUserActionDelegate(CancellationToken ct);

        private readonly RealmClock realmClock;
        private readonly IWebRequestController webRequestController;
        private readonly RequestUserActionDelegate requestUserAction;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        public EnsureClockSync(RealmClock realmClock,
            IWebRequestController webRequestController,
            RequestUserActionDelegate requestUserAction,
            IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.realmClock = realmClock;
            this.webRequestController = webRequestController;
            this.requestUserAction = requestUserAction;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            Result response = Result.RESTART;

            while (response == Result.RESTART)
            {
                await TryProbeServerTimeAsync(ct);

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

        private async UniTask TryProbeServerTimeAsync(CancellationToken ct)
        {
            if (realmClock.HasSample) return;

            try
            {
                // This will internally set the realm clock on success
                await webRequestController.IsHeadReachableAsync(
                    ReportCategory.STARTUP,
                    URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.Host)),
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
