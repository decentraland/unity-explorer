using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Utils
{
    public static class LookDirectionUtils
    {
        private const float COS_1_DEG = 0.9998477f; // cos(1 degree)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 FlattenLookDirection(Vector3 forward, Vector3 up, float cosThreshold = COS_1_DEG)
        {
            float dotUp = Vector3.Dot(forward, Vector3.up);
            Vector3 direction = Mathf.Abs(dotUp) > cosThreshold ? -up : forward;
            direction.y = 0;
            return direction.normalized;
        }
    }
}
