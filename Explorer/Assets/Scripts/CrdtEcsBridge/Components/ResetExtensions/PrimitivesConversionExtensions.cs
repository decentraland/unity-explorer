using DCL.ECSComponents;
using Decentraland.Common;
using System;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

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

        public static Color PBColorToUnityColor(Color3 protoColor, float alphaValue = 1) =>
            new ()
            {
                r = protoColor.R,
                g = protoColor.G,
                b = protoColor.B,
                a = alphaValue,
            };

        public static Color PBColorToUnityColor(Color4 protoColor) =>
            new ()
            {
                r = protoColor.R,
                g = protoColor.G,
                b = protoColor.B,
                a = protoColor.A,
            };

        public static LightShadows PBLightSourceShadowToUnityLightShadow(PBLightSource.Types.ShadowType shadow)
        {
            switch (shadow)
            {
                case PBLightSource.Types.ShadowType.StNone: return LightShadows.None;
                case PBLightSource.Types.ShadowType.StSoft: return LightShadows.Soft;
                case PBLightSource.Types.ShadowType.StHard: return LightShadows.Hard;
                default: throw new ArgumentOutOfRangeException(nameof(shadow), shadow, null);
            }
        }
    }
}
