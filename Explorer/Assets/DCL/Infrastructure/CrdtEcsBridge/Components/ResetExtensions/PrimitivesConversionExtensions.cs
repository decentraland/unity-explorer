using Decentraland.Common;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;
using Vector2 = UnityEngine.Vector2;

namespace CrdtEcsBridge.Components.Conversion
{
    /// <summary>
    ///     Custom logic to convert PBVectors and PBQuaternions onto Unity Vectors and Quaternions
    /// </summary>
    public static class PrimitivesConversionExtensions
    {
        public static Vector3 ToUnityVector(this Decentraland.Common.Vector3 protoVector) =>
            new ()
            {
                x = protoVector.X,
                y = protoVector.Y,
                z = protoVector.Z,
            };

        public static Decentraland.Common.Vector3 ToProtoVector(this Vector3 unityVector) =>
            new ()
            {
                X = unityVector.x,
                Y = unityVector.y,
                Z = unityVector.z,
            };

        public static Quaternion ToUnityQuaternion(this Decentraland.Common.Quaternion protoQuaternion) =>
            new ()
            {
                x = protoQuaternion.X,
                y = protoQuaternion.Y,
                z = protoQuaternion.Z,
                w = protoQuaternion.W,
            };

        public static Vector2 ToUnityVector(this Decentraland.Common.Vector2 protoVector) =>
            new ()
            {
                x = protoVector.X,
                y = protoVector.Y,
            };

        public static Decentraland.Common.Quaternion ToProtoQuaternion(this Quaternion unityQuaternion) =>
            new ()
            {
                X = unityQuaternion.x,
                Y = unityQuaternion.y,
                Z = unityQuaternion.z,
                W = unityQuaternion.w,
            };

        public static Color ToUnityColor(this Color3 protoColor, float alphaValue = 1) =>
            new ()
            {
                r = protoColor.R,
                g = protoColor.G,
                b = protoColor.B,
                a = alphaValue,
            };

        public static Color ToUnityColor(this Color4 protoColor) =>
            new ()
            {
                r = protoColor.R,
                g = protoColor.G,
                b = protoColor.B,
                a = protoColor.A,
            };
    }
}
