using Decentraland.Common;

namespace DCL.ECSComponents
{
    public partial class PBPrimaryPointerInfo
    {
        public void Initialize()
        {
            ScreenDelta ??= new Vector2();
            ScreenCoordinates ??= new Vector2();
            WorldRayDirection ??= new Vector3();
        }
    }
}
