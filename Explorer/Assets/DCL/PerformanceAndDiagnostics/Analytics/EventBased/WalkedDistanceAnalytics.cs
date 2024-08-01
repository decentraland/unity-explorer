using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Animation;
using DCL.Utilities;
using Segment.Serialization;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class WalkedDistanceAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;

        private CancellationTokenSource cts;
        private AvatarAnimationEventsHandler animEventsHandler;

        private long stepCount;
        private bool isDisposed;

        public WalkedDistanceAnalytics(IAnalyticsController analytics, ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy)
        {
            this.analytics = analytics;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
        }

        public void Initialize()
        {
            cts = new CancellationTokenSource();
            SubscribeToPlayerStepAsync(cts.Token).Forget();

            Application.wantsToQuit += () =>
            {
                SendAnalytics();
                return true;
            };

            Application.quitting += SendAnalytics;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => SendAnalytics();
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            GC.SuppressFinalize(this);

            SendAnalytics();

            if (animEventsHandler != null)
                animEventsHandler.PlayerStepped -= OnPlayerStep;

            cts?.SafeCancelAndDispose();
        }

        ~WalkedDistanceAnalytics()
        {
            Dispose();
        }

        private async UniTask SubscribeToPlayerStepAsync(CancellationToken ct)
        {
            await UniTask.WaitUntil(() => mainPlayerAvatarBaseProxy.Configured, PlayerLoopTiming.LastPostLateUpdate, ct);
            await UniTask.WaitUntil(() => mainPlayerAvatarBaseProxy.StrictObject != null, PlayerLoopTiming.LastPostLateUpdate, ct);

            animEventsHandler = mainPlayerAvatarBaseProxy.StrictObject.AvatarAnimator.gameObject.GetComponent<AvatarAnimationEventsHandler>();

            if (animEventsHandler != null)
                animEventsHandler.PlayerStepped += OnPlayerStep;
        }

        private void OnPlayerStep()
        {
            stepCount++;
        }

        private void SendAnalytics()
        {
            if (stepCount == 0) return;

            analytics.Track(AnalyticsEvents.Badges.WALKED_DISTANCE, new JsonObject
            {
                { "step_count", stepCount },
            });

            stepCount = 0;
        }
    }
}
