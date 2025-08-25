using System;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Decentraland.Terrain
{
    public static class GeometryExtensions
    {
        public static float3 GetCorner(this MinMaxAABB bounds, int index)
        {
            switch (index)
            {
                case 0b000: return bounds.Min;
                case 0b001: return float3(bounds.Min.xy, bounds.Max.z);
                case 0b010: return float3(bounds.Min.x, bounds.Max.y, bounds.Min.z);
                case 0b011: return float3(bounds.Min.x, bounds.Max.yz);
                case 0b100: return float3(bounds.Max.x, bounds.Min.yz);
                case 0b101: return float3(bounds.Max.x, bounds.Min.y, bounds.Max.z);
                case 0b110: return float3(bounds.Max.xy, bounds.Min.z);
                case 0b111: return bounds.Max;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static float3 MultiplyPoint(this float4x4 matrix, float3 point)
        {
            float4 temp = mul(matrix, float4(point, 1f));
            return temp.xyz * (1f / temp.w);
        }

        public static MinMaxAABB ToMinMaxAABB(this Bounds bounds) =>
            new (bounds.min, bounds.max);

        public static Vector2Int ToVector2Int(this int2 value) =>
            new (value.x, value.y);

        public static Vector2 XZ(this Vector3 value) =>
            new (value.x, value.z);
    }
}
