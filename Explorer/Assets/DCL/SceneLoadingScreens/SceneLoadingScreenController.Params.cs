using DCL.AsyncLoadReporting;
using System;

namespace DCL.SceneLoadingScreens
{
    public partial class SceneLoadingScreenController
    {
        public struct Params
        {
            // TODO: in the future we may require the parcel to show specific scene tips
            // public Vector2Int Coordinate { get; }
            public IAsyncLoadProcessReport AsyncLoadProcessReport { get; }
            public TimeSpan Timeout { get; }

            public Params(IAsyncLoadProcessReport asyncLoadProcessReport,
                TimeSpan timeout)
            {
                AsyncLoadProcessReport = asyncLoadProcessReport;
                Timeout = timeout;
            }
        }
    }
}
