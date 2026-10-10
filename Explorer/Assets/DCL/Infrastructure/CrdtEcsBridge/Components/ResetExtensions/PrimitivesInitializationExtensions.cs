using Decentraland.Common;

namespace CrdtEcsBridge.Components.ResetExtensions
{
    /// <summary>
    ///     Custom logic to initialize components to default values without fields reallocation
    /// </summary>
    public static class PrimitivesInitializationExtensions
    {
        public static void Reset(this Vector3 protoVector) =>
            protoVector.Set(UnityEngine.Vector3.zero);

        public static void Set(this Vector3 protoVector, UnityEngine.Vector3 unityVector)
        {
            protoVector.X = unityVector.x;
            protoVector.Y = unityVector.y;
            protoVector.Z = unityVector.z;
        }

        public static void Set(this Quaternion protoQuaternion, UnityEngine.Quaternion unityQuaternion)
        {
            protoQuaternion.X = unityQuaternion.x;
            protoQuaternion.Y = unityQuaternion.y;
            protoQuaternion.Z = unityQuaternion.z;
            protoQuaternion.W = unityQuaternion.w;
        }
    }
}
