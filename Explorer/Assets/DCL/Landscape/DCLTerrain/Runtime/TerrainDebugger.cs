using System;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Decentraland.Terrain
{
    public sealed class TerrainDebugger : MonoBehaviour
    {
        [field: SerializeField] internal TerrainData TerrainData { get; private set; }

        private static Vector3[] parcelCorners = new Vector3[4];

        private void OnDrawGizmos()
        {
            if (TerrainData == null)
                return;

            float3 parcelCenter = float3(0f, TerrainData.MaxHeight * 0.5f, 0f);
            parcelCenter.xz = ((float2)Parcel + 0.5f) * TerrainData.ParcelSize;
            Gizmos.DrawWireCube(parcelCenter, (float3)TerrainData.ParcelSize);
        }

        internal int2 Parcel => (int2)floor(transform.position.XZ() / TerrainData.ParcelSize);
    }
}
