using DCL.AsyncLoadReporting;

namespace DCL.SceneLoadingScreens
{
    public partial class SceneLoadingScreenController
    {
        public struct Params
        {
            // TODO: in the future we may require the parcel to show specific scene tips
            // public Vector2Int Coordinate { get; }
            public AsyncLoadProcessReport AsyncLoadProcessReport { get; }

            public Params(AsyncLoadProcessReport asyncLoadProcessReport)
            {
                AsyncLoadProcessReport = asyncLoadProcessReport;
            }
        }
    }
}
