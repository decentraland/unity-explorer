using DCL.DebugUtilities;

namespace DCL.WebRequests.Analytics
{
    public interface IRequestMetric
    {
        public DebugLongMarkerDef.Unit GetUnit();
        public ulong GetMetric();
        public void OnRequestStarted(ITypedWebRequest request);
        public void OnRequestEnded(ITypedWebRequest request);
    }
}
