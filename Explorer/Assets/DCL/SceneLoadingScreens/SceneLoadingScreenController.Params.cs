using DCL.AsyncLoadReporting;
using System.Threading;

namespace DCL.SceneLoadingScreens
{
    public partial class SceneLoadingScreenController
    {
        public struct Params
        {
            // TODO: in the future we may require the parcel to show specific scene tips
            // public Vector2Int Coordinate { get; }
            public AsyncLoadProcessReport AsyncLoadProcessReport { get; }

            /// <summary>
            ///     This cancellation token will be fired if the loading process has finished and the loading screen should disappear gracefully
            /// </summary>
            public CancellationToken LoadingProcessIsFinished;

            public Params(AsyncLoadProcessReport asyncLoadProcessReport, CancellationToken loadingProcessIsFinished)
            {
                AsyncLoadProcessReport = asyncLoadProcessReport;
                LoadingProcessIsFinished = loadingProcessIsFinished;
            }
        }
    }
}
