using DCL.ECSComponents;
using Decentraland.Common;
using System.Diagnostics.CodeAnalysis;

namespace CrdtEcsBridge.Components.Special
{
    /// <summary>
    ///     Custom logic to initialize components to default values without fields reallocation
    /// </summary>
    public static class ComponentInitializationExtensions
    {
        /// <summary>
        ///     Ensures reference type fields are initialized only once and reset to default values
        /// </summary>
        /// <param name="protoRaycastHit"></param>
        public static void Reset(this RaycastHit protoRaycastHit)
        {
            // Vectors in proto are reference types!
            protoRaycastHit.Position ??= new Vector3();
            Reset(protoRaycastHit.Position);
            protoRaycastHit.Direction ??= new Vector3();
            Reset(protoRaycastHit.Direction);
            protoRaycastHit.GlobalOrigin ??= new Vector3();
            Reset(protoRaycastHit.GlobalOrigin);
            protoRaycastHit.NormalHit ??= new Vector3();
            Reset(protoRaycastHit.NormalHit);
        }

        public static void Reset(this PBRaycastResult protoRaycastResult)
        {
            // Can't pool Repeated Fields (no setter, readonly)
            protoRaycastResult.Hits.Clear();

            protoRaycastResult.Direction ??= new Vector3();
            Reset(protoRaycastResult.Direction);
            protoRaycastResult.GlobalOrigin ??= new Vector3();
            Reset(protoRaycastResult.GlobalOrigin);
        }

        public static void Reset(this Vector3 protoVector) =>
            protoVector.Set(UnityEngine.Vector3.zero);

        public static void Set([NotNull] this Vector3 protoVector, UnityEngine.Vector3 unityVector)
        {
            protoVector.X = unityVector.x;
            protoVector.Y = unityVector.y;
            protoVector.Z = unityVector.z;
        }
    }
}
