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

        public override void OnRequestStarted(ITypedWebRequest request, DateTime startTime) { }

        public override void OnRequestEnded(ITypedWebRequest request, TimeSpan duration) =>
            counter++;
    }
}
