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

        private SegmentAnalyticsService net = null!;
        private RustSegmentAnalyticsService rust = null!;

        private void Start()
        {
            string key = Environment.GetEnvironmentVariable("SEGMENT_WRITE_KEY")!;

#pragma warning disable CS0618
            net = new SegmentAnalyticsService(new Configuration(key));
#pragma warning restore CS0618
            rust = new RustSegmentAnalyticsService(key);

            var token = destroyCancellationToken;
            FlushAsync(token).Forget();
            TrackAsync(token).Forget();
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
    }
}
