using DCL.DebugUtilities;

namespace DCL.WebRequests.Analytics.Metrics
{
    internal class ActiveCounter : IRequestMetric
    {
        private ulong counter { get; set; }

        public DebugLongMarkerDef.Unit GetUnit() => DebugLongMarkerDef.Unit.NoFormat;

        public ulong GetMetric() =>
            counter;

        void IRequestMetric.OnRequestStarted(ITypedWebRequest request, IWebRequestAnalytics webRequestAnalytics, IWebRequest webRequest)
        {
            counter++;
        }

        void IRequestMetric.OnRequestEnded(ITypedWebRequest request, IWebRequestAnalytics webRequestAnalytics, IWebRequest webRequest)
        {
            counter--;
        }
    }
}
