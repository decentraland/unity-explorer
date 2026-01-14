using DCL.DebugUtilities;

namespace DCL.WebRequests.Analytics
{
    public interface IRequestMetric
    {
        /// <summary>
        ///     Should be updated every frame
        /// </summary>
        void Update() { }

        public DebugLongMarkerDef.Unit GetUnit();

        public ulong GetMetric();

        public void OnRequestStarted(ITypedWebRequest request);

        public void OnRequestEnded(ITypedWebRequest request);
    }
}
