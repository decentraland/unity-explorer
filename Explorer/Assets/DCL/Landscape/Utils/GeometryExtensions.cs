using System;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;
using static Unity.Mathematics.math;

namespace DCL.Landscape.Utils
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

        public static Bounds ToBounds(this MinMaxAABB aabb)
        {
            Bounds bounds = default;
            bounds.SetMinMax(aabb.Min, aabb.Max);
            return bounds;
        }
    }
}
