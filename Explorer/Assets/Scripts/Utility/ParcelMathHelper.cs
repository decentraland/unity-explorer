using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Utility
{
    public static class ParcelMathHelper
    {
        public readonly struct ParcelCorners
        {
            public readonly Vector3 minXZ;
            public readonly Vector3 minXmaxZ;
            public readonly Vector3 maxXZ;
            public readonly Vector3 maxXminZ;

            public ParcelCorners(Vector3 minXZ, Vector3 minXmaxZ, Vector3 maxXZ, Vector3 maxXminZ)
            {
                this.minXZ = minXZ;
                this.minXmaxZ = minXmaxZ;
                this.maxXZ = maxXZ;
                this.maxXminZ = maxXminZ;
            }
        }

        public const float PARCEL_SIZE = 16.0f;

        public const float SQR_PARCEL_SIZE = PARCEL_SIZE * PARCEL_SIZE;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 ToInt2(this Vector2Int vector2Int) =>
            new (vector2Int.x, vector2Int.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int ToVector2Int(this int2 int2) =>
            new (int2.x, int2.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetSceneRelativePosition(Vector3 position, Vector3 scenePosition) =>
            position - scenePosition;

        public static Vector3 GetPositionByParcelPosition(Vector2Int parcelPosition) =>
            new (parcelPosition.x * PARCEL_SIZE, 0.0f, parcelPosition.y * PARCEL_SIZE);

        public static ParcelCorners CalculateCorners(Vector2Int parcelPosition)
        {
            Vector3 min = GetPositionByParcelPosition(parcelPosition);
            return new ParcelCorners(min, min + new Vector3(0, 0, PARCEL_SIZE), min + new Vector3(PARCEL_SIZE, 0, PARCEL_SIZE), min + new Vector3(PARCEL_SIZE, 0, 0));
        }

        public static Vector2Int FloorToParcel(Vector3 position) =>
            new (Mathf.FloorToInt(position.x / PARCEL_SIZE), Mathf.FloorToInt(position.z / PARCEL_SIZE));

        public static void ParcelsInRange(Vector3 position, int loadRadius, HashSet<int2> results)
        {
            float range = loadRadius * PARCEL_SIZE;
            var focus = new Vector2(position.x, position.z);

            Vector2 minPoint = focus - new Vector2(range, range);
            Vector2 maxPoint = focus + new Vector2(range, range);

            var minParcel = Vector2Int.FloorToInt(minPoint / PARCEL_SIZE);
            var maxParcel = Vector2Int.CeilToInt(maxPoint / PARCEL_SIZE);

            results.Clear();

            for (int parcelX = minParcel.x; parcelX < maxParcel.x; ++parcelX)
            {
                for (int parcelY = minParcel.y; parcelY < maxParcel.y; ++parcelY)
                {
                    Vector2 parcel = new Vector2(parcelX, parcelY);
                    Vector2 parcelMinPoint = parcel * PARCEL_SIZE;
                    Vector2 parcelMaxPoint = (parcel + new Vector2(1.0f, 1.0f)) * PARCEL_SIZE;

                    float nearestPointX = Mathf.Clamp(focus.x, parcelMinPoint.x, parcelMaxPoint.x);
                    float nearestPointY = Mathf.Clamp(focus.y, parcelMinPoint.y, parcelMaxPoint.y);
                    Vector2 nearestPoint = new Vector2(nearestPointX, nearestPointY);
                    float distance = Vector2.Distance(nearestPoint, focus);

                    if (distance < range) results.Add(new int2(parcelX, parcelY));
                }
            }
        }
    }
}
