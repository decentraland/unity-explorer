﻿using DCL.Diagnostics;
using Segment.Analytics;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public enum AnalyticsMode
    {
        DEBUG_LOG,
        SEGMENT,
        DISABLED,
    }

    [CreateAssetMenu(fileName = "AnalyticsConfiguration", menuName = "DCL/Diagnostics/Analytics Configuration")]
    public class AnalyticsConfiguration : ScriptableObject
    {
        private const string SEGMENT_WRITE_KEY = "SEGMENT_WRITE_KEY";

        [SerializeField]
        [Tooltip("If true, the AnalyticsConfiguration will attempt to use the local environment variable as a fallback if the write key is not set.")]
        private bool useLocalEnvVariableFallback;

        [SerializeField]
        [Tooltip("This parameter specifies the maximum number of messages to be queued before they are flushed (i.e., sent to the server). "
                 + "For example, if flushSize is set to 20, Segment will batch up to 20 messages before sending them in a single request.")]
        private int flushSize = 20;

        [SerializeField]
        [Tooltip("This parameter sets the interval (in seconds) at which Segment attempts to flush the queued messages. "
                 + "Even if the queue does not reach the flushSize limit, messages will still be sent after this interval has passed.")]
        private int flushInterval = 30;

        [SerializeField] [HideInInspector]
        private string segmentWriteKey;

        [field: SerializeField]
        [Tooltip("This parameter sets the interval (in seconds) at which the performance report is tracked to the analytics.")]
        public float PerformanceReportInterval { get; private set; } = 1.0f;

        [field: SerializeField]
        public AnalyticsMode Mode { get; private set; } = AnalyticsMode.SEGMENT;

        private Configuration segmentConfiguration;

        public int FlushSize => flushSize;

        public int FlushInterval => flushInterval;

        public bool TryGetSegmentConfiguration(out Configuration configuration)
        {
            if (segmentConfiguration != null)
            {
                configuration = segmentConfiguration;
                return true;
            }

            if (string.IsNullOrEmpty(segmentWriteKey) && !TryGetWriteKeyLocally())
            {
                configuration = null;
                return false;
            }

            return TryCreateSegmentConfiguration(out configuration);
        }

        private bool TryGetWriteKeyLocally()
        {
            if (useLocalEnvVariableFallback)
            {
                ReportHub.LogWarning(ReportCategory.ANALYTICS, "Segment Write Key is not set. Fall down to local environment variable.");
                segmentWriteKey = Environment.GetEnvironmentVariable(SEGMENT_WRITE_KEY);

                if (!string.IsNullOrEmpty(segmentWriteKey))
                    return true;
            }

            ReportHub.LogWarning(ReportCategory.ANALYTICS, $"{SEGMENT_WRITE_KEY} environment variable is not set.");
            return false;
        }

        private bool TryCreateSegmentConfiguration(out Configuration configuration)
        {
            try { segmentConfiguration = new Configuration(segmentWriteKey, new SegmentErrorHandler(), flushSize, flushInterval); }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.ANALYTICS, $"Cannot create Segment configuration with provided write key (incorrect key?). Exception {e.Message}.");
                configuration = null;
                return false;
            }

            ReportHub.Log(ReportCategory.ANALYTICS, "Segment configuration successfully created.");
            configuration = segmentConfiguration;
            return true;
        }

        public void SetWriteKey(string writeKey) =>
            segmentWriteKey = writeKey;
    }
}
