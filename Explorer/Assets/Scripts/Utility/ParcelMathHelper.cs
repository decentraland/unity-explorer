using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Utility
{
    public static class ParcelMathHelper
    {
        public const float PARCEL_SIZE = 16.0f;
        public const float SQR_PARCEL_SIZE = PARCEL_SIZE * PARCEL_SIZE;

        public static readonly SceneGeometry UNDEFINED_SCENE_GEOMETRY = new (
            Vector3.zero,
            new SceneCircumscribedPlanes(float.MinValue, float.MaxValue, float.MinValue, float.MaxValue));

        public static readonly Vector3 RoadPivotDeviation = new (8, 0, 8);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 ToInt2(this Vector2Int vector2Int) =>
            new (vector2Int.x, vector2Int.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int ToVector2Int(this int2 int2) =>
            new (int2.x, int2.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetSceneRelativePosition(Vector3 position, Vector3 scenePosition) =>
            position - scenePosition;

        public static Vector3 GetPositionByParcelPosition(Vector2Int parcelPosition, bool adaptYPositionToTerrain = false)
        {
            var position = new Vector3(parcelPosition.x * PARCEL_SIZE, 0.0f, parcelPosition.y * PARCEL_SIZE);

            if (adaptYPositionToTerrain)
            {
                const float TERRAIN_HEIGHT_ADAPTATION_OFFSET = 2.0f;
                position.y = GetNearestSurfaceHeight(position) + TERRAIN_HEIGHT_ADAPTATION_OFFSET;
            }

            return position;
        }

        private static float GetNearestSurfaceHeight(Vector3 position) =>
            Physics.Raycast(position + (Vector3.up * 100), Vector3.down, out RaycastHit hit) ? hit.point.y : position.y;

        /// <summary>
        ///     Creates scene geometry from multiple occupied parcels
        /// </summary>
        public static SceneGeometry CreateSceneGeometry(IReadOnlyList<ParcelCorners> parcelsCorners, Vector2Int baseParcel)
        {
            float circumscribedPlaneMinX = float.MaxValue;
            float circumscribedPlaneMaxX = float.MinValue;
            float circumscribedPlaneMinZ = float.MaxValue;
            float circumscribedPlaneMaxZ = float.MinValue;

            for (var i = 0; i < parcelsCorners.Count; i++)
            {
                ParcelCorners corners = parcelsCorners[i];

                circumscribedPlaneMinX = Mathf.Min(corners.minXZ.x, circumscribedPlaneMinX);
                circumscribedPlaneMaxX = Mathf.Max(corners.maxXZ.x, circumscribedPlaneMaxX);
                circumscribedPlaneMinZ = Mathf.Min(corners.minXZ.z, circumscribedPlaneMinZ);
                circumscribedPlaneMaxZ = Mathf.Max(corners.maxXZ.z, circumscribedPlaneMaxZ);
            }

            // to prevent on-boundary flickering (float accuracy) extend the circumscribed planes a little bit

            const float EXTEND_AMOUNT = 0.05f;

            circumscribedPlaneMinX -= EXTEND_AMOUNT;
            circumscribedPlaneMaxX += EXTEND_AMOUNT;
            circumscribedPlaneMinZ -= EXTEND_AMOUNT;
            circumscribedPlaneMaxZ += EXTEND_AMOUNT;

            Vector3 baseParcelPosition = GetPositionByParcelPosition(baseParcel);

            return new SceneGeometry(
                baseParcelPosition,
                new SceneCircumscribedPlanes(circumscribedPlaneMinX, circumscribedPlaneMaxX, circumscribedPlaneMinZ, circumscribedPlaneMaxZ));
        }

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
                    var parcel = new Vector2(parcelX, parcelY);
                    Vector2 parcelMinPoint = parcel * PARCEL_SIZE;
                    Vector2 parcelMaxPoint = (parcel + new Vector2(1.0f, 1.0f)) * PARCEL_SIZE;

                    float nearestPointX = Mathf.Clamp(focus.x, parcelMinPoint.x, parcelMaxPoint.x);
                    float nearestPointY = Mathf.Clamp(focus.y, parcelMinPoint.y, parcelMaxPoint.y);
                    var nearestPoint = new Vector2(nearestPointX, nearestPointY);
                    float distance = Vector2.Distance(nearestPoint, focus);

                    if (distance < range) results.Add(new int2(parcelX, parcelY));
                }
            }
        }

        public static Vector2Int WorldToGridPosition(Vector3 worldPosition) =>
            new (
                (int)Mathf.Floor(worldPosition.x / PARCEL_SIZE),
                (int)Mathf.Floor(worldPosition.z / PARCEL_SIZE)
            );

        public static Vector2 WorldToGridPositionUnclamped(Vector3 worldPosition) =>
            new (
                worldPosition.x / PARCEL_SIZE,
                worldPosition.z / PARCEL_SIZE
            );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Intersects(this in SceneCircumscribedPlanes boundingPlanes, Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            return boundingPlanes.MinX < min.x && boundingPlanes.MaxX > max.x
                                               && boundingPlanes.MinZ < min.z && boundingPlanes.MaxZ > max.z;
        }

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

        /// <summary>
        ///     Rectangular area around all scene parcels
        /// </summary>
        public readonly struct SceneCircumscribedPlanes
        {
            public readonly float MinX;
            public readonly float MaxX;
            public readonly float MinZ;
            public readonly float MaxZ;

            public SceneCircumscribedPlanes(float minX, float maxX, float minZ, float maxZ)
            {
                MinX = minX;
                MaxX = maxX;
                MinZ = minZ;
                MaxZ = maxZ;
            }
        }

        public readonly struct SceneGeometry
        {
            /// <summary>
            ///     Scene is instantiated at this position
            /// </summary>
            public readonly Vector3 BaseParcelPosition;

            /// <summary>
            ///     <inheritdoc cref="SceneCircumscribedPlanes" />
            /// </summary>
            public readonly SceneCircumscribedPlanes CircumscribedPlanes;

            public SceneGeometry(Vector3 baseParcelPosition, SceneCircumscribedPlanes circumscribedPlanes)
            {
                BaseParcelPosition = baseParcelPosition;
                CircumscribedPlanes = circumscribedPlanes;
            }
        }
    }
}
