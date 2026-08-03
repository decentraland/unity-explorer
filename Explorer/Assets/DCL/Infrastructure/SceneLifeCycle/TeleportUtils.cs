using Arch.Core;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
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

        public static (Vector3 targetWorldPosition, Vector3? cameraTarget) PickTargetWithOffset(SceneEntityDefinition? sceneDef, Vector2Int parcel, string? spawnPointName = null)
        {
            Vector3? cameraTarget = null;

            Vector3 parcelBaseWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel).WithErrorCompensation();
            Vector3 targetWorldPosition = parcelBaseWorldPosition;

            List<SceneMetadata.SpawnPoint>? spawnPoints = sceneDef?.metadata.spawnPoints;

            if (sceneDef != null && spawnPoints is { Count: > 0 })
            {
                Vector3 anchorWorldPosition;
                LocalBounds bounds;

                if (TryPickNamedSpawnPoint(spawnPoints, spawnPointName, out SceneMetadata.SpawnPoint spawnPoint))
                {
                    // Named spawn point positions are scene-local: anchor them at the scene base parcel,
                    // not at the teleport target parcel
                    Vector2Int baseParcel = sceneDef.metadata.scene.DecodedBase;
                    anchorWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(baseParcel).WithErrorCompensation();
                    bounds = CalculateLocalBounds(sceneDef.metadata.scene.DecodedParcels, baseParcel);
                }
                else
                {
                    anchorWorldPosition = parcelBaseWorldPosition;
                    bounds = CalculateLocalBounds(sceneDef.metadata.scene.DecodedParcels, parcel);
                    spawnPoint = PickSpawnPoint(spawnPoints, targetWorldPosition, parcelBaseWorldPosition, in bounds);
                }

                targetWorldPosition = anchorWorldPosition + GetSpawnPositionOffset(spawnPoint, in bounds);

                if (spawnPoint.cameraTarget != null)
                    cameraTarget = spawnPoint.cameraTarget!.Value.ToVector3() + anchorWorldPosition;
            }

            return (targetWorldPosition, cameraTarget);
        }

        /// <summary>
        ///     Names the spawn point the creator placed in <paramref name="parcel" />, so a teleport aimed at that
        ///     parcel can address it through <see cref="PickTargetWithOffset" /> instead of guessing a spot itself.
        ///     Several spawn points reaching into the parcel are narrowed down by the ordinary rules of
        ///     <see cref="PickSpawnPoint" />. A nameless spawn point is not addressable and counts as absent.
        /// </summary>
        public static bool TryPickSpawnPointNameInParcel(SceneEntityDefinition sceneDef, Vector2Int parcel, out string spawnPointName)
        {
            spawnPointName = string.Empty;

            List<SceneMetadata.SpawnPoint>? spawnPoints = sceneDef.metadata.spawnPoints;

            if (spawnPoints is not { Count: > 0 })
                return false;

            Vector2Int baseParcel = sceneDef.metadata.scene.DecodedBase;
            LocalBounds bounds = CalculateLocalBounds(sceneDef.metadata.scene.DecodedParcels, baseParcel);

            // The parcel expressed in the same scene-local space as the spawn point coordinates
            Vector2 parcelMin = new Vector2((parcel.x - baseParcel.x) * ParcelMathHelper.PARCEL_SIZE,
                                            (parcel.y - baseParcel.y) * ParcelMathHelper.PARCEL_SIZE);

            List<SceneMetadata.SpawnPoint> inParcel = ListPool<SceneMetadata.SpawnPoint>.Get();

            foreach (SceneMetadata.SpawnPoint spawnPoint in spawnPoints)
                if (CoversParcel(spawnPoint, in bounds, parcelMin))
                    inParcel.Add(spawnPoint);

            if (inParcel.Count > 0)
            {
                Vector3 baseWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(baseParcel).WithErrorCompensation();
                Vector3 parcelWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel).WithErrorCompensation();
                spawnPointName = PickSpawnPoint(inParcel, parcelWorldPosition, baseWorldPosition, in bounds).name;
            }

            ListPool<SceneMetadata.SpawnPoint>.Release(inParcel);

            return !string.IsNullOrEmpty(spawnPointName);
        }

        /// <summary>
        ///     True when the span <paramref name="spawnPoint" /> can resolve to reaches into the parcel whose
        ///     scene-local minimum corner is <paramref name="parcelMin" />. Spawn point coordinates are
        ///     scene-local, so clamp them to the scene bounds exactly as <see cref="PickTargetWithOffset" /> does.
        ///     Borders belong to both neighbours: a spawn point sitting on a parcel edge counts as inside it.
        /// </summary>
        private static bool CoversParcel(SceneMetadata.SpawnPoint spawnPoint, in LocalBounds bounds, Vector2 parcelMin)
        {
            // An unset coordinate resolves to the parcel centre, as GetSpawnPositionOffset does
            if (!TryGetClampedRange(spawnPoint.position.x, bounds.MinX, bounds.MaxX, out float minX, out float maxX))
                minX = maxX = ParcelMathHelper.HALF_PARCEL_SIZE;

            if (!TryGetClampedRange(spawnPoint.position.z, bounds.MinZ, bounds.MaxZ, out float minZ, out float maxZ))
                minZ = maxZ = ParcelMathHelper.HALF_PARCEL_SIZE;

            return maxX >= parcelMin.x && minX <= parcelMin.x + ParcelMathHelper.PARCEL_SIZE
                                       && maxZ >= parcelMin.y && minZ <= parcelMin.y + ParcelMathHelper.PARCEL_SIZE;
        }

        /// <summary>
        ///     The span a spawn point coordinate can resolve to, clamped to the axis bounds. False when the
        ///     coordinate is absent from the scene metadata, leaving the fallback to the caller — the spawn
        ///     position substitutes the parcel centre horizontally but the ground vertically.
        /// </summary>
        private static bool TryGetClampedRange(SceneMetadata.SpawnPoint.Coordinate coordinate, float axisMin, float axisMax, out float min, out float max)
        {
            if (coordinate.SingleValue != null)
            {
                min = max = Mathf.Clamp(coordinate.SingleValue.Value, axisMin, axisMax);
                return true;
            }

            float[]? range = coordinate.MultiValue;

            if (range == null)
            {
                min = max = 0f;
                return false;
            }

            switch (range.Length)
            {
                case 0:
                    min = max = 0f;
                    return true;
                case 1:
                    min = max = Mathf.Clamp(range[0], axisMin, axisMax);
                    return true;
                default:
                    min = range[0];
                    max = range[1];

                    if (min > max)
                        (min, max) = (max, min);

                    min = Mathf.Clamp(min, axisMin, axisMax);
                    max = Mathf.Clamp(max, axisMin, axisMax);
                    return true;
            }
        }

        private static bool TryPickNamedSpawnPoint(IReadOnlyList<SceneMetadata.SpawnPoint> spawnPoints, string? spawnPointName, out SceneMetadata.SpawnPoint spawnPoint)
        {
            spawnPoint = default(SceneMetadata.SpawnPoint);

            if (string.IsNullOrEmpty(spawnPointName))
                return false;

            var namedIndex = -1;

            for (var i = 0; i < spawnPoints.Count; i++)
            {
                if (!string.Equals(spawnPoints[i].name, spawnPointName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (namedIndex < 0)
                    namedIndex = i;
                else
                {
                    ReportHub.LogWarning(ReportCategory.SCENE_LOADING, $"Scene declares multiple spawn points named '{spawnPointName}', using the first one");
                    break;
                }
            }

            if (namedIndex >= 0)
            {
                spawnPoint = spawnPoints[namedIndex];
                return true;
            }

            ReportHub.LogWarning(ReportCategory.SCENE_LOADING, $"Spawn point '{spawnPointName}' not found in scene, falling back to default spawn point selection");
            return false;
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
            // Scatter the players over the whole span the creator declared instead of stacking them on one point
            static float? GetSpawnComponentClamped(SceneMetadata.SpawnPoint.Coordinate coordinate, float axisMin, float axisMax)
            {
                if (!TryGetClampedRange(coordinate, axisMin, axisMax, out float min, out float max))
                    return null;

                if (Mathf.Approximately(min, max))
                    return max;

                return (float)((RANDOM.NextDouble() * (max - min)) + min);
            }

            return new Vector3(
                GetSpawnComponentClamped(spawnPoint.position.x, bounds.MinX, bounds.MaxX) ?? ParcelMathHelper.HALF_PARCEL_SIZE,
                GetSpawnComponentClamped(spawnPoint.position.y, 0f, float.PositiveInfinity) ?? 0,
                GetSpawnComponentClamped(spawnPoint.position.z, bounds.MinZ, bounds.MaxZ) ?? ParcelMathHelper.HALF_PARCEL_SIZE);
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

