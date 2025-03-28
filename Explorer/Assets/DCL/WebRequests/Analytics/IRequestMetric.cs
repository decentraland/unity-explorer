using DCL.DebugUtilities;

namespace DCL.WebRequests.Analytics
{
    public interface IRequestMetric
    {
        DebugLongMarkerDef.Unit GetUnit();

        ulong GetMetric();

        internal void OnRequestStarted(ITypedWebRequest request, IWebRequest webRequest);

        internal void OnRequestEnded(ITypedWebRequest request, IWebRequest webRequest);
    }
}
