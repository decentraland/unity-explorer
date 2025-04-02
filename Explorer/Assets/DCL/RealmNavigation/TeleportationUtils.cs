using DCL.Ipfs;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Random = System.Random;

namespace DCL.RealmNavigation
{
    public static class TeleportationUtils
    {
        private const string TRAM_LINE_TITLE = "Tram Line";
        private static readonly Random RANDOM = new ();

        public static (Vector3 targetWorldPosition, Vector3? cameraTarget) PickTargetWithOffset(SceneEntityDefinition? sceneDef, Vector2Int parcel)
        {
            Vector3? cameraTarget = null;
            Vector3 targetWorldPosition;

            if (sceneDef == null || IsTramLine(sceneDef.metadata.OriginalJson.AsSpan()))
            {
                targetWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel).WithErrorCompensation().WithTerrainOffset();
                return (targetWorldPosition, cameraTarget);
            }
            else
            {
                Vector3 parcelBaseWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel).WithErrorCompensation();
                targetWorldPosition = parcelBaseWorldPosition;

                List<SceneMetadata.SpawnPoint>? spawnPoints = sceneDef.metadata.spawnPoints;

                if (spawnPoints is { Count: > 0 })
                {
                    SceneMetadata.SpawnPoint spawnPoint = PickSpawnPoint(spawnPoints, targetWorldPosition, parcelBaseWorldPosition);

                    // TODO validate offset position is within bounds of one of scene parcels
                    targetWorldPosition += GetSpawnPositionOffset(spawnPoint);

                    if (spawnPoint.cameraTarget != null)
                        cameraTarget = spawnPoint.cameraTarget!.Value.ToVector3() + parcelBaseWorldPosition;
                }

                return (targetWorldPosition, cameraTarget);
            }
        }

        public static bool IsTramLine(ReadOnlySpan<char> originalJson) =>
            ExtractTitleValue(originalJson).SequenceEqual(TRAM_LINE_TITLE.AsSpan());

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

        private static SceneMetadata.SpawnPoint PickSpawnPoint(IReadOnlyList<SceneMetadata.SpawnPoint> spawnPoints, Vector3 targetWorldPosition, Vector3 parcelBaseWorldPosition)
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
                    Vector3 spawnWorldPosition = GetSpawnPositionOffset(sp) + parcelBaseWorldPosition;
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

        private static Vector3 GetSpawnPositionOffset(SceneMetadata.SpawnPoint spawnPoint)
        {
            static float GetRandomPoint(float[] coordArray)
            {
                float randomPoint = 0;

                switch (coordArray.Length)
                {
                    case 1:
                        randomPoint = coordArray[0];
                        break;
                    case >= 2:
                    {
                        float min = coordArray[0];
                        float max = coordArray[1];

                        if (Mathf.Approximately(min, max))
                            return max;

                        if (min > max)
                            (min, max) = (max, min);

                        randomPoint = (float)((RANDOM.NextDouble() * (max - min)) + min);
                        break;
                    }
                }

                return randomPoint;
            }

            static float? GetSpawnComponent(SceneMetadata.SpawnPoint.Coordinate coordinate)
            {
                if (coordinate.SingleValue != null)
                    return coordinate.SingleValue.Value;

                if (coordinate.MultiValue != null)
                    return GetRandomPoint(coordinate.MultiValue);

                return null;
            }

            return new Vector3(
                GetSpawnComponent(spawnPoint.position.x) ?? ParcelMathHelper.PARCEL_SIZE / 2f,
                GetSpawnComponent(spawnPoint.position.y) ?? 0,
                GetSpawnComponent(spawnPoint.position.z) ?? ParcelMathHelper.PARCEL_SIZE / 2f);
        }
    }
}
