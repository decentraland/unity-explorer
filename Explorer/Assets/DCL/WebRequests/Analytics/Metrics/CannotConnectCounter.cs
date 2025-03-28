using DCL.DebugUtilities;

namespace DCL.WebRequests.Analytics.Metrics
{
    internal class CannotConnectCounter : IRequestMetric
    {
        private ulong counter;

        public DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.NoFormat;

        public ulong GetMetric() =>
            counter;

        void IRequestMetric.OnRequestStarted(ITypedWebRequest request, IWebRequest webRequest) { }

        void IRequestMetric.OnRequestEnded(ITypedWebRequest request, IWebRequest wr)
        {
            if (!wr.Response.IsSuccess && wr.Response.Error.Contains(WebRequestUtils.CANNOT_CONNECT_ERROR))
                counter++;
        }
    }
}
