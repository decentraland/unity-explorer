using DCL.DebugUtilities.UIBindings;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    public readonly struct MetricRegistration
    {
        public readonly struct AggregatedMetric
        {
            private readonly IReadOnlyList<IRequestMetric> metrics;
            private readonly MetricAggregationMode aggregationMode;
            private readonly ElementBinding<ulong>? debugBinding;

            public AggregatedMetric(IReadOnlyList<IRequestMetric> metrics, MetricAggregationMode aggregationMode, ElementBinding<ulong>? debugBinding)
            {
                this.metrics = metrics;
                this.aggregationMode = aggregationMode;
                this.debugBinding = debugBinding;
            }

            public void UpdateBinding()
            {
                if (debugBinding == null) return;

                switch (aggregationMode)
                {
                    case MetricAggregationMode.SUM:
                        ulong sum = 0;

                        foreach (IRequestMetric requestMetric in metrics)
                            sum += requestMetric.GetMetric();

                        debugBinding!.Value = sum;
                        break;
                }
            }
        }

        public readonly struct RequestType
        {
            public readonly Type Type;
            public readonly string MarkerName;

            public RequestType(Type type, string markerName)
            {
                Type = type;
                MarkerName = markerName;
            }
        }

        public readonly struct MetricType
        {
            public readonly Type Type;
            public readonly MetricAggregationMode AggregationMode;

            public MetricType(Type type, MetricAggregationMode aggregationMode = MetricAggregationMode.NONE)
            {
                Type = type;
                AggregationMode = aggregationMode;
            }
        }

        internal readonly IRequestMetric metric;

        /// <summary>
        ///     Will be `null` if the debug category is disabled (in the build)
        /// </summary>
        private readonly ElementBinding<ulong>? debugBinding;

        public MetricRegistration(IRequestMetric metric, ElementBinding<ulong>? debugBinding)
        {
            this.metric = metric;
            this.debugBinding = debugBinding;
        }

        public void UpdateBinding()
        {
            if (debugBinding != null) debugBinding.Value = metric.GetMetric();
        }
    }
}
