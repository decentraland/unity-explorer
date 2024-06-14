using Unity.Mathematics;
using Utility;

namespace DCL.Utilities.Extensions
{
    public static class VectorExtensions
    {
        public static float2 ToFloat2(this UnityEngine.Vector3 vector) =>
            new (vector.x, vector.z);

        public static bool IsInside(this float2 value, ParcelMathHelper.ParcelCorners corners) =>
            value.x >= corners.minXZ.x && value.x <= corners.maxXZ.x &&
            value.y >= corners.minXZ.z && value.y <= corners.maxXZ.z;
    }
}
