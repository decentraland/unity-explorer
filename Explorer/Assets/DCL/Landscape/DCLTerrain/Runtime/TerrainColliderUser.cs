using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Decentraland.Terrain
{
    public sealed class TerrainColliderUser : MonoBehaviour
    {
        private static readonly List<float2> POSITIONS_XZ = new ();
        private static readonly List<Transform> TRANSFORMS = new ();

        private void OnEnable() =>
            TRANSFORMS.Add(transform);

        private void OnDisable() =>
            TRANSFORMS.RemoveSwapBack(transform);

        internal static List<float2> GetPositionsXZ()
        {
            POSITIONS_XZ.Clear();
            POSITIONS_XZ.EnsureCapacity(TRANSFORMS.Count);

            for (var i = 0; i < TRANSFORMS.Count; i++)
            {
                Vector3 position = TRANSFORMS[i].position;
                POSITIONS_XZ.Add(float2(position.x, position.z));
            }

            return POSITIONS_XZ;
        }
    }
}
