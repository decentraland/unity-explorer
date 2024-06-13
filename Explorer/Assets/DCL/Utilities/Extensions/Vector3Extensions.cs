using Unity.Mathematics;

namespace DCL.Utilities.Extensions
{
    public static class Vector3Extensions
    {
        public static float2 ToFloat2(this UnityEngine.Vector3 vector) =>
            new (vector.x, vector.z);
    }
}
