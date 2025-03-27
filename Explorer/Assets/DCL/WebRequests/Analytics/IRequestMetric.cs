using DCL.DebugUtilities;

namespace DCL.WebRequests.Analytics
{
    public interface IRequestMetric
    {
        DebugLongMarkerDef.Unit GetUnit();

        ulong GetMetric();

        internal void OnRequestStarted(ITypedWebRequest request, IWebRequestAnalytics webRequestAnalytics, IWebRequest webRequest);

        internal void OnRequestEnded(ITypedWebRequest request, IWebRequestAnalytics webRequestAnalytics, IWebRequest webRequest);
    }
}
