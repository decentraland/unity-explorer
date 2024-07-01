using Segment.Analytics;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsGroup
    {
        public string Name;
        public List<AnalyticsEvent> Events;
    }

    public class AnalyticsEvent
    {
        public string Name;
    }

    [CreateAssetMenu(fileName = "AnalyticsConfiguration", menuName = "DCL/AnalyticsConfiguration", order = 0)]
    public class AnalyticsConfiguration : ScriptableObject
    {
        private const string SEGMENT_WRITE_KEY = "SEGMENT_WRITE_KEY";

        [SerializeField]
        [Tooltip("This parameter specifies the maximum number of messages to be queued before they are flushed (i.e., sent to the server). "
                 + "For example, if flushSize is set to 20, Segment will batch up to 20 messages before sending them in a single request.")]
        private int flushSize = 20;

        [SerializeField]
        [Tooltip("This parameter sets the interval (in seconds) at which Segment attempts to flush the queued messages. "
                 + "Even if the queue does not reach the flushSize limit, messages will still be sent after this interval has passed.")]
        private int flushInterval = 30;

        [field: SerializeField]
        [Tooltip("This parameter sets the interval (in seconds) at which the performance report is tracked to the analytics.")]
        public float PerformanceReportInterval { get; private set; } = 1.0f;

        private string segmentWriteKey;
        private Configuration segmentConfiguration;

        public Configuration SegmentConfiguration => segmentConfiguration ??=
            new Configuration(WriteKey, new ErrorHandler(), flushSize, flushInterval);

        public string WriteKey
        {
            private get
            {
                if (string.IsNullOrEmpty(segmentWriteKey))
                {
                    segmentWriteKey = Environment.GetEnvironmentVariable(SEGMENT_WRITE_KEY);

                    if (string.IsNullOrEmpty(segmentWriteKey))
                        throw new InvalidOperationException($"{SEGMENT_WRITE_KEY} environment variable is not set.");
                }

                return segmentWriteKey;
            }

            set => segmentWriteKey = value;
        }

        public bool EventIsEnabled(string eventName)
        {
            return true;
        }
    }
}
