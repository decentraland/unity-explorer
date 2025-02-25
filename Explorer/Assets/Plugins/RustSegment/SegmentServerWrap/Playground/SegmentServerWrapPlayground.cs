using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using Segment.Serialization;
using System;
using UnityEngine;

namespace Plugins.RustSegment.SegmentServerWrap.Playground
{
    public class SegmentServerWrapPlayground : MonoBehaviour
    {
        [SerializeField] private bool fillMode;
        [SerializeField] private long unFlushedBatches;

        private IAnalyticsService service = null!;

        private void Start()
        {
            Initialize();
            Identify();
        }

        public void Update()
        {
            if (fillMode)
                Track();

            unFlushedBatches = (long) NativeMethods.SegmentServerUnFlushedBatchesCount();
        }

        [ContextMenu(nameof(Initialize))]
        private void Initialize()
        {
            string key = "WqI7aI1XeKUBlHtMJXZSo5dneDQE2mGD";

            if (string.IsNullOrWhiteSpace(key))
                throw new Exception("Segment Write Key is not set.");

            service = new RustSegmentAnalyticsService(key);
        }

        [ContextMenu(nameof(Identify))]
        public void Identify()
        {
            service.Identify(
                "check_user_id",
                new JsonObject
                {
                    ["env"] = "test",
                }
            );
        }

        [ContextMenu(nameof(Track))]
        public void Track()
        {
            var curly = new JsonObject
            {
                { "works", "yes" },
            };

            var bracket = new JsonObject
            {
                ["works"] = "yes"
            };

            service.Track(
                "TEST_SHARP",
                curly
            );

            ReportHub.Log(ReportData.UNSPECIFIED, $"Curly {curly}, Bracket {bracket}");
        }

        [ContextMenu(nameof(Flush))]
        public void Flush()
        {
            service.Flush();
        }
    }
}
