using Arch.Core;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Random = System.Random;

namespace ECS.SceneLifeCycle
{
    public static class TeleportUtils
    {
        private const string TRAM_LINE_TITLE = "Tram Line";
        private const string LONG_ROAD_TITLE = "Long Road";
        private static readonly Random RANDOM = new ();

        public static bool IsRoad(string sceneTitle) =>
            string.Equals(sceneTitle, TRAM_LINE_TITLE, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sceneTitle, LONG_ROAD_TITLE, StringComparison.OrdinalIgnoreCase);

        public static bool IsRoad(ReadOnlySpan<char> originalJson)
        {
            ReadOnlySpan<char> span = ExtractTitleValue(originalJson);

            return span.SequenceEqual(TRAM_LINE_TITLE.AsSpan())
                   || span.SequenceEqual(LONG_ROAD_TITLE.AsSpan());
        }

        private static ReadOnlySpan<char> ExtractTitleValue(ReadOnlySpan<char> json)
        {
            int titleIndex = json.IndexOf(@"""title"":");

            if (titleIndex == -1)
                return ReadOnlySpan<char>.Empty;

            // Move to the start of the title value (after "title": ")
            int valueStartIndex = json[titleIndex..].IndexOf(':') + 1;
            ReadOnlySpan<char> valueSpan = json.Slice(titleIndex + valueStartIndex);

            int openQuoteIndex = valueSpan.IndexOf('"');

            if (openQuoteIndex == -1)
                return ReadOnlySpan<char>.Empty;

            int closeQuoteIndex = valueSpan[(openQuoteIndex + 1)..].IndexOf('"');

            if (closeQuoteIndex == -1)
                return ReadOnlySpan<char>.Empty;

            return valueSpan.Slice(openQuoteIndex + 1, closeQuoteIndex);
        }

        public static PlayerTeleportingState GetTeleportParcel(World world, Entity playerEntity)
        {
            var teleportParcel = new PlayerTeleportingState();

            if (world.TryGet(playerEntity, out PlayerTeleportIntent playerTeleportIntent))
            {
                teleportParcel.IsTeleporting = true;
                teleportParcel.Parcel = playerTeleportIntent.Parcel;
            }

            if (world.TryGet(playerEntity, out PlayerTeleportIntent.JustTeleported justTeleported))
            {
                teleportParcel.IsTeleporting = true;
                teleportParcel.Parcel = justTeleported.Parcel;
            }

            return teleportParcel;
        }

        public static (Vector3 targetWorldPosition, Vector3? cameraTarget) PickTargetWithOffset(SceneEntityDefinition? sceneDef, Vector2Int parcel)
        {
            Vector3? cameraTarget = null;

            Vector3 parcelBaseWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel).WithErrorCompensation();
            Vector3 targetWorldPosition = parcelBaseWorldPosition;

            List<SceneMetadata.SpawnPoint>? spawnPoints = sceneDef?.metadata.spawnPoints;

            if (sceneDef != null && spawnPoints is { Count: > 0 })
            {
                LocalBounds bounds = CalculateLocalBounds(sceneDef.metadata.scene.DecodedParcels, parcel);

                SceneMetadata.SpawnPoint spawnPoint = PickSpawnPoint(spawnPoints, targetWorldPosition, parcelBaseWorldPosition, in bounds);

                targetWorldPosition += GetSpawnPositionOffset(spawnPoint, in bounds);

                if (spawnPoint.cameraTarget != null)
                    cameraTarget = spawnPoint.cameraTarget!.Value.ToVector3() + parcelBaseWorldPosition;
            }

            return (targetWorldPosition, cameraTarget);
        }

        private static SceneMetadata.SpawnPoint PickSpawnPoint(IReadOnlyList<SceneMetadata.SpawnPoint> spawnPoints, Vector3 targetWorldPosition, Vector3 parcelBaseWorldPosition, in LocalBounds bounds)
        {
            List<SceneMetadata.SpawnPoint> defaults = ListPool<SceneMetadata.SpawnPoint>.Get();
            defaults.AddRange(spawnPoints.Where(sp => sp.@default));

            IReadOnlyList<SceneMetadata.SpawnPoint> elegibleSpawnPoints = defaults.Count > 0 ? defaults : spawnPoints;
            var closestIndex = 0;

            if (elegibleSpawnPoints.Count > 1)
            {
                float closestDistance = float.MaxValue;

                for (var i = 0; i < elegibleSpawnPoints.Count; i++)
                {
                    SceneMetadata.SpawnPoint sp = elegibleSpawnPoints[i];
                    Vector3 spawnWorldPosition = GetSpawnPositionOffset(sp, in bounds) + parcelBaseWorldPosition;
                    float distance = Vector3.Distance(targetWorldPosition, spawnWorldPosition);

                    if (distance < closestDistance)
                    {
                        closestIndex = i;
                        closestDistance = distance;
                    }
                }
            }

            SceneMetadata.SpawnPoint spawnPoint = elegibleSpawnPoints[closestIndex];

            ListPool<SceneMetadata.SpawnPoint>.Release(defaults);

            return spawnPoint;
        }

        private static Vector3 GetSpawnPositionOffset(SceneMetadata.SpawnPoint spawnPoint, in LocalBounds bounds)
        {
            static float GetRandomPointClamped(float[] coordArray, float axisMin, float axisMax)
            {
                switch (coordArray.Length)
                {
                    case 1:
                        return Mathf.Clamp(coordArray[0], axisMin, axisMax);
                    case >= 2:
                    {
                        float min = coordArray[0];
                        float max = coordArray[1];

                        if (min > max)
                            (min, max) = (max, min);

                        min = Mathf.Clamp(min, axisMin, axisMax);
                        max = Mathf.Clamp(max, axisMin, axisMax);

                        if (Mathf.Approximately(min, max))
                            return max;

                        return (float)((RANDOM.NextDouble() * (max - min)) + min);
                    }
                    default:
                        return 0;
                }
            }

            static float? GetSpawnComponentClamped(SceneMetadata.SpawnPoint.Coordinate coordinate, float axisMin, float axisMax)
            {
                if (coordinate.SingleValue != null)
                    return Mathf.Clamp(coordinate.SingleValue.Value, axisMin, axisMax);

                if (coordinate.MultiValue != null)
                    return GetRandomPointClamped(coordinate.MultiValue, axisMin, axisMax);

                return null;
            }

            return new Vector3(
                GetSpawnComponentClamped(spawnPoint.position.x, bounds.MinX, bounds.MaxX) ?? ParcelMathHelper.PARCEL_SIZE / 2f,
                GetSpawnComponentClamped(spawnPoint.position.y, 0f, float.PositiveInfinity) ?? 0,
                GetSpawnComponentClamped(spawnPoint.position.z, bounds.MinZ, bounds.MaxZ) ?? ParcelMathHelper.PARCEL_SIZE / 2f);
        }

        private static LocalBounds CalculateLocalBounds(IReadOnlyList<Vector2Int> sceneParcels, Vector2Int referenceParcel)
        {
            if (sceneParcels.Count == 0)
                return new LocalBounds(0, ParcelMathHelper.PARCEL_SIZE, 0, ParcelMathHelper.PARCEL_SIZE);

            int minParcelX = int.MaxValue;
            int maxParcelX = int.MinValue;
            int minParcelY = int.MaxValue;
            int maxParcelY = int.MinValue;

            for (var i = 0; i < sceneParcels.Count; i++)
            {
                Vector2Int p = sceneParcels[i];
                if (p.x < minParcelX) minParcelX = p.x;
                if (p.x > maxParcelX) maxParcelX = p.x;
                if (p.y < minParcelY) minParcelY = p.y;
                if (p.y > maxParcelY) maxParcelY = p.y;
            }

            return new LocalBounds(
                (minParcelX - referenceParcel.x) * ParcelMathHelper.PARCEL_SIZE,
                (maxParcelX - referenceParcel.x + 1) * ParcelMathHelper.PARCEL_SIZE,
                (minParcelY - referenceParcel.y) * ParcelMathHelper.PARCEL_SIZE,
                (maxParcelY - referenceParcel.y + 1) * ParcelMathHelper.PARCEL_SIZE);
        }

        private readonly struct LocalBounds
        {
            public readonly float MinX;
            public readonly float MaxX;
            public readonly float MinZ;
            public readonly float MaxZ;

            public LocalBounds(float minX, float maxX, float minZ, float maxZ)
            {
                MinX = minX;
                MaxX = maxX;
                MinZ = minZ;
                MaxZ = maxZ;
            }
        }


        public struct PlayerTeleportingState
        {
            public Vector2Int Parcel;
            public bool IsTeleporting;
        }
    }
}

