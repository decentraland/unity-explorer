using DCL.DebugUtilities.UIBindings;
using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace DCL.WebRequests.Analytics
{
    public readonly struct MetricRegistration
    {
        public class AggregatedMetric
        {
            private readonly IReadOnlyList<IRequestMetric> metrics;
            private readonly MetricAggregationType aggregationMode;
            private readonly ElementBinding<ulong>? debugBinding;

            private ulong previousSum;

            public AggregatedMetric(IReadOnlyList<IRequestMetric> metrics, MetricAggregationType aggregationMode, ElementBinding<ulong>? debugBinding)
            {
                this.metrics = metrics;
                this.aggregationMode = aggregationMode;
                this.debugBinding = debugBinding;
            }

            public void UpdateBinding()
            {
                if (debugBinding == null) return;

                switch (aggregationMode.AggregationMode)
                {
                    case MetricAggregationMode.SUM:
                    case MetricAggregationMode.SUM_PER_FRAME:
                        ulong sum = 0;

                        foreach (IRequestMetric requestMetric in metrics)
                            sum += requestMetric.GetMetric();

                        ulong bindingValue;

                        if (aggregationMode.AggregationMode == MetricAggregationMode.SUM_PER_FRAME)
                        {
                            bindingValue = sum - previousSum;
                            previousSum = sum;
                        }
                        else
                            bindingValue = sum;

                        debugBinding!.Value = bindingValue;

                        ProfilerCounterValue<ulong>? profilerCounterValue = aggregationMode.ProfilerCounterValue;

                        if (profilerCounterValue != null)
                        {
                            ProfilerCounterValue<ulong> profilerCounterValueValue = profilerCounterValue.Value;
                            profilerCounterValueValue.Value = bindingValue;
                        }

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
            public readonly MetricAggregationType[] AggregationModes;

            public MetricType(Type type, params MetricAggregationType[] aggregationModes)
            {
                Type = type;
                AggregationModes = aggregationModes;
            }
        }

        public readonly struct MetricAggregationType
        {
            public readonly MetricAggregationMode AggregationMode;
            public readonly ProfilerCounterValue<ulong>? ProfilerCounterValue;

            public MetricAggregationType(MetricAggregationMode aggregationMode, ProfilerCounterValue<ulong>? profilerCounterValue)
            {
                AggregationMode = aggregationMode;
                ProfilerCounterValue = profilerCounterValue;
            }

            public static implicit operator MetricAggregationType(MetricAggregationMode mode) =>
                new (mode, null);
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
