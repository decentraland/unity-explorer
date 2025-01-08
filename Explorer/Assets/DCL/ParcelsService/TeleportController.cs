using Arch.Core;
using Arch.System;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Random = System.Random;
using SpawnPoint = DCL.Ipfs.SceneMetadata.SpawnPoint;

namespace DCL.ParcelsService
{
    public class TeleportController : ITeleportController
    {
        private delegate void PickTargetDelegate(SceneEntityDefinition? sceneDef, ref Vector2Int parcel, out Vector3 targetWorldPosition, out Vector3? cameraTarget);

        private const string TRAM_LINE_TITLE = "Tram Line";
        private static readonly Random RANDOM = new ();

        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly SceneAssetLock sceneAssetLock;

        private IRetrieveScene? retrieveScene;
        private World? world;
        private Entity cameraEntity;
        private Entity playerEntity;

        public IRetrieveScene SceneProviderStrategy
        {
            set => retrieveScene = value;
        }

        public World World
        {
            set
            {
                world = value;
                cameraEntity = world.CacheCamera();
                playerEntity = world.CachePlayer();
            }
        }

        public TeleportController(ISceneReadinessReportQueue sceneReadinessReportQueue, SceneAssetLock sceneAssetLock)
        {
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.sceneAssetLock = sceneAssetLock;
        }

        public void InvalidateRealm()
        {
            retrieveScene = null;
        }

        private async UniTask<WaitForSceneReadiness?> TeleportAsync(Vector2Int parcel, PickTargetDelegate pickTargetDelegate,
            AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            // if current scene is still loading it will block the teleport until its assets are resolved or timed out
            sceneAssetLock.Reset();

            if (retrieveScene == null)
            {
                TeleportCharacter(new PlayerTeleportIntent(ParcelMathHelper.GetPositionByParcelPosition(parcel, true), parcel, ct, loadReport));
                loadReport.SetProgress(1f);
                return null;
            }

            SceneEntityDefinition? sceneDef = await retrieveScene.ByParcelAsync(parcel, ct);

            pickTargetDelegate(sceneDef, ref parcel, out Vector3 targetWorldPosition, out Vector3? cameraTarget);

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            TeleportCharacter(new PlayerTeleportIntent(targetWorldPosition, parcel, ct, loadReport));

            if (cameraTarget != null)
            {
                ForceCameraLookAt(new CameraLookAtIntent(cameraTarget.Value, targetWorldPosition));
                ForceCharacterLookAt(new PlayerLookAtIntent(cameraTarget.Value, targetWorldPosition));
            }

            if (sceneDef == null)
            {
                // Instant completion for empty parcels
                loadReport.SetProgress(1f);

                return null;
            }

            return new WaitForSceneReadiness(parcel, loadReport, sceneReadinessReportQueue);
        }

        public UniTask<WaitForSceneReadiness?> TeleportToSceneSpawnPointAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            // if current scene is still loading it will block the teleport until its assets are resolved or timed out
            return TeleportAsync(parcel, PickTarget, loadReport, ct);

            static void PickTarget(SceneEntityDefinition? sceneDef, ref Vector2Int parcel, out Vector3 targetWorldPosition, out Vector3? cameraTarget)
            {
                cameraTarget = null;

                if (sceneDef != null && !IsTramLine(sceneDef.metadata.OriginalJson.AsSpan()))
                {
                    // Override parcel as it's a new target
                    parcel = sceneDef.metadata.scene.DecodedBase;
                    Vector3 parcelBaseWorldPosition = GetPositionByParcelPositionWithErrorCompensation(parcel);
                    targetWorldPosition = parcelBaseWorldPosition;

                    List<SpawnPoint>? spawnPoints = sceneDef.metadata.spawnPoints;

                    if (spawnPoints is { Count: > 0 })
                    {
                        SpawnPoint spawnPoint = PickSpawnPoint(spawnPoints, targetWorldPosition, parcelBaseWorldPosition);

                        // TODO validate offset position is within bounds of one of scene parcels
                        targetWorldPosition += GetSpawnPositionOffset(spawnPoint);

                        if (spawnPoint.cameraTarget != null)
                            cameraTarget = spawnPoint.cameraTarget!.Value.ToVector3() + parcelBaseWorldPosition;
                    }
                }
                else
                    targetWorldPosition = GetPositionByParcelPositionWithErrorCompensation(parcel, true);
            }
        }

        private static bool IsTramLine(ReadOnlySpan<char> originalJson) =>
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

        /// <summary>
        ///     Pulls position a little bit towards the center of the parcel to compensate a possible float error
        ///     that shifts position outside the parcel
        /// </summary>
        private static Vector3 GetPositionByParcelPositionWithErrorCompensation(Vector2Int parcel, bool adaptYPositionToTerrain = false)
        {
            const float EPSILON = 0.0001f;

            return ParcelMathHelper.GetPositionByParcelPosition(parcel, adaptYPositionToTerrain) + new Vector3(EPSILON, 0, EPSILON);
        }

        public UniTask TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            return TeleportAsync(parcel, PickTarget, loadReport, ct);

            static void PickTarget(SceneEntityDefinition? sceneDef, ref Vector2Int parcel, out Vector3 targetWorldPosition, out Vector3? cameraTarget)
            {
                targetWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel);
                cameraTarget = null;

                if (sceneDef != null)

                    // Override parcel as it's a new target
                    parcel = sceneDef.metadata.scene.DecodedBase;
            }
        }

        private static SpawnPoint PickSpawnPoint(IReadOnlyList<SpawnPoint> spawnPoints, Vector3 targetWorldPosition, Vector3 parcelBaseWorldPosition)
        {
            List<SpawnPoint> defaults = ListPool<SpawnPoint>.Get();
            defaults.AddRange(spawnPoints.Where(sp => sp.@default));

            IReadOnlyList<SpawnPoint> elegibleSpawnPoints = defaults.Count > 0 ? defaults : spawnPoints;
            var closestIndex = 0;

            if (elegibleSpawnPoints.Count > 1)
            {
                float closestDistance = float.MaxValue;

                for (var i = 0; i < elegibleSpawnPoints.Count; i++)
                {
                    SpawnPoint sp = elegibleSpawnPoints[i];
                    Vector3 spawnWorldPosition = GetSpawnPositionOffset(sp) + parcelBaseWorldPosition;
                    float distance = Vector3.Distance(targetWorldPosition, spawnWorldPosition);

                    if (distance < closestDistance)
                    {
                        closestIndex = i;
                        closestDistance = distance;
                    }
                }
            }

            SpawnPoint spawnPoint = elegibleSpawnPoints[closestIndex];

            ListPool<SpawnPoint>.Release(defaults);

            return spawnPoint;
        }

        private static Vector3 GetSpawnPositionOffset(SpawnPoint spawnPoint)
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

            static float? GetSpawnComponent(SpawnPoint.Coordinate coordinate)
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

        private void TeleportCharacter(PlayerTeleportIntent intent)
        {
            world?.AddOrGet(playerEntity, intent);
        }

        private void ForceCameraLookAt(CameraLookAtIntent intent)
        {
            world?.AddOrGet(cameraEntity, intent);
        }

        private void ForceCharacterLookAt(PlayerLookAtIntent intent)
        {
            world?.AddOrGet(playerEntity, intent);
        }
    }
}
