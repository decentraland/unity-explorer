using DCL.DebugUtilities;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class ActiveCounter : IRequestMetric
    {
        private ulong counter { get; set; }

        public DebugLongMarkerDef.Unit GetUnit() => DebugLongMarkerDef.Unit.NoFormat;

        public ulong GetMetric() =>
            counter;

        public void OnRequestStarted(ITypedWebRequest request)
        {
            counter++;
        }

        public void OnRequestEnded(ITypedWebRequest request)
        {
            counter--;
        }
    }
}
