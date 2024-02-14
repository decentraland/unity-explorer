using UnityEngine;

namespace CrdtEcsBridge.Components.Conversion
{
    /// <summary>
    ///     Custom logic to convert PBVectors and PBQuaternions onto Unity Vectors and Quaternions
    /// </summary>
    public static class PrimitivesConversionExtensions
    {
        public static Vector3 PBVectorToUnityVector(Decentraland.Common.Vector3 protoVector) =>
            new ()
            {
                x = protoVector.X,
                y = protoVector.Y,
                z = protoVector.Z,
            };

        public static Quaternion PBQuaternionToUnityQuaternion(Decentraland.Common.Quaternion protoQuaternion) =>
            new ()
            {
                x = protoQuaternion.X,
                y = protoQuaternion.Y,
                z = protoQuaternion.Z,
                w = protoQuaternion.W,
            };
    }
}
