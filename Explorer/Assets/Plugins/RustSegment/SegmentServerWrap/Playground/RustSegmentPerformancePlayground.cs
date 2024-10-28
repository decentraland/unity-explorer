using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics.Analytics;
using Segment.Analytics;
using Segment.Serialization;
using System;
using System.Threading;
using UnityEngine;

namespace Plugins.RustSegment.SegmentServerWrap.Playground
{
    public class RustSegmentPerformancePlayground : MonoBehaviour
    {
        private enum Mode
        {
            Net,
            Rust
        }

        [SerializeField] private Mode mode;
        [SerializeField] private float delayBetweenRequests = 0.1f;
        [SerializeField] private float delayBetweenFlushes = 5f;
        [SerializeField] private int brakesMilliseconds = 100;

        private SegmentAnalyticsService net = null!;
        private RustSegmentAnalyticsService rust = null!;

        private void Start()
        {
            string key = Environment.GetEnvironmentVariable("SEGMENT_WRITE_KEY")!;
            SetUp(key);
        }

        private void Update()
        {
            Thread.Sleep(brakesMilliseconds);
        }

        private void SetUp(string key)
        {
            if (mode is Mode.Net)
#pragma warning disable CS0618
                net = new SegmentAnalyticsService(new Configuration(key));
#pragma warning restore CS0618
            if (mode is Mode.Rust)
                rust = new RustSegmentAnalyticsService(key);
        }

        private async UniTaskVoid TrackAsync(CancellationToken token)
        {
            var message = new JsonObject { { "mode", mode.ToString() } };

            while (token.IsCancellationRequested == false)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delayBetweenRequests), cancellationToken: token);
                CurrentService().Track("PERFORMANCE_TEST", message);
            }
        }

        private async UniTaskVoid FlushAsync(CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delayBetweenFlushes), cancellationToken: token);
                CurrentService().Flush();
            }
        }

        private IAnalyticsService CurrentService() =>
            mode switch
            {
                Mode.Net => net,
                Mode.Rust => rust,
                _ => throw new ArgumentOutOfRangeException()
            };

        [ContextMenu(nameof(LaunchLoop))]
        public void LaunchLoop()
        {
            var token = destroyCancellationToken;
            FlushAsync(token).Forget();
            TrackAsync(token).Forget();
        }

        [ContextMenu(nameof(EnqueueTracks))]
        public void EnqueueTracks()
        {
            const int COUNT = 15;

            for (var i = 0; i < COUNT; i++)
                CurrentService().Track("FLUSH_TEST", new JsonObject { { "mode", mode.ToString() } });
        }

        [ContextMenu(nameof(MeasureFlush))]
        public void MeasureFlush()
        {
            CurrentService().Flush();
        }
    }
}
