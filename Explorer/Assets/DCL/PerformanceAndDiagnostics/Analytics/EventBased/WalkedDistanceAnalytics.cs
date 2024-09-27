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
        private const float SEND_INTERVAL = 15;

        private readonly IAnalyticsController analytics;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;

        private CancellationTokenSource cts;
        private AvatarAnimationEventsHandler animEventsHandler;

        private bool isDisposed;

        private float countdown = SEND_INTERVAL;
        public long StepCount { get; private set; }

        public WalkedDistanceAnalytics(IAnalyticsController analytics, ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy)
        {
            this.analytics = analytics;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
        }

        public void Initialize()
        {
            cts = new CancellationTokenSource();
            SubscribeToPlayerStepAsync(cts.Token).Forget();

            Application.quitting += Dispose;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
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

        public void Update(float deltaTime)
        {
            countdown -= deltaTime;

            if (countdown <= 0 || StepCount > 100)
            {
                SendAnalytics();
                Reset();
            }
        }

        private void SendAnalytics()
        {
            if (StepCount == 0) return;

            analytics.Track(AnalyticsEvents.Badges.WALKED_DISTANCE, new JsonObject
            {
                { "step_count", StepCount },
            });
        }

        public void Reset()
        {
            StepCount = 0;
            countdown = SEND_INTERVAL;
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
            StepCount++;
        }
    }
}
