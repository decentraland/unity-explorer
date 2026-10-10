using DCL.ECSComponents;
using Decentraland.Common;

namespace CrdtEcsBridge.Components.ResetExtensions
{
    public static class RaycastResetExtensions
    {
        /// <summary>
        ///     Ensures reference type fields are initialized only once and reset to default values
        /// </summary>
        /// <param name="protoRaycastHit"></param>
        public static RaycastHit Reset(this RaycastHit protoRaycastHit)
        {
            // Vectors in proto are reference types!
            protoRaycastHit.Position ??= new Vector3();
            protoRaycastHit.Position.Reset();
            protoRaycastHit.Direction ??= new Vector3();
            protoRaycastHit.Direction.Reset();
            protoRaycastHit.GlobalOrigin ??= new Vector3();
            protoRaycastHit.GlobalOrigin.Reset();
            protoRaycastHit.NormalHit ??= new Vector3();
            protoRaycastHit.NormalHit.Reset();
            return protoRaycastHit;
        }

        public static PBRaycastResult Reset(this PBRaycastResult protoRaycastResult)
        {
            // Can't pool Repeated Fields (no setter, readonly)
            protoRaycastResult.Hits.Clear();

            protoRaycastResult.Direction ??= new Vector3();
            protoRaycastResult.Direction.Reset();
            protoRaycastResult.GlobalOrigin ??= new Vector3();
            protoRaycastResult.GlobalOrigin.Reset();
            return protoRaycastResult;
        }
    }
}
