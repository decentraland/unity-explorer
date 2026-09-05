using DCL.DebugUtilities;
using System;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class Total : RequestMetricBase
    {
        private ulong counter { get; set; }

        public override DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.NoFormat;

        public override ulong GetMetric() =>
            counter;

        public override void OnRequestStarted<T>(T request, DateTime startTime) { }

        public override void OnRequestEnded<T>(T request, TimeSpan duration) =>
            counter++;
    }
}
