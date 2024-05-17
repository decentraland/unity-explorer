using Arch.Core;
using Arch.System;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using ECS.SceneLifeCycle.Reporting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using SpawnPoint = DCL.Ipfs.SceneMetadata.SpawnPoint;

namespace DCL.ParcelsService
{
    public partial class TeleportController : ITeleportController
    {
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private IRetrieveScene? retrieveScene;
        private World? world;

        public IRetrieveScene SceneProviderStrategy
        {
            set => retrieveScene = value;
        }

        public World World
        {
            set => world = value;
        }

        public TeleportController(ISceneReadinessReportQueue sceneReadinessReportQueue)
        {
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
        }

        public void InvalidateRealm()
        {
            retrieveScene = null;
        }

        public async UniTask<WaitForSceneReadiness?> TeleportToSceneSpawnPointAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            if (retrieveScene == null)
            {
                TeleportCharacterQuery(world, new PlayerTeleportIntent(ParcelMathHelper.GetPositionByParcelPosition(parcel, true), parcel, loadReport));
                loadReport.SetProgress(1f);
                return null;
            }

            SceneEntityDefinition? sceneDef = await retrieveScene.ByParcelAsync(parcel, ct);

            Vector3 targetWorldPosition;
            Vector3? cameraTarget = null;

            if (sceneDef != null)
            {
                // Override parcel as it's a new target
                parcel = sceneDef.metadata.scene.DecodedBase;
                Vector3 parcelBaseWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel);
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
                targetWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel, true);

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            TeleportCharacterQuery(retrieveScene.World, new PlayerTeleportIntent(targetWorldPosition, parcel, loadReport));

            if (cameraTarget != null)
            {
                ForceCameraLookAtQuery(retrieveScene.World, new CameraLookAtIntent(cameraTarget.Value, targetWorldPosition));
                ForceCharacterLookAtQuery(retrieveScene.World, new PlayerLookAtIntent(cameraTarget.Value, targetWorldPosition));
            }

            if (sceneDef == null)
            {
                // Instant completion for empty parcels
                loadReport.SetProgress(1f);

                return null;
            }

            return new WaitForSceneReadiness(parcel, loadReport, sceneReadinessReportQueue);
        }

        // TODO: this method should be removed, implies possible mantainance efforts and its only for debugging purposes
        public async UniTask TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            if (retrieveScene == null)
            {
                TeleportCharacterQuery(world, new PlayerTeleportIntent(ParcelMathHelper.GetPositionByParcelPosition(parcel, true), parcel, loadReport));
                loadReport.SetProgress(1f);
                return;
            }

            Vector3 characterPos = ParcelMathHelper.GetPositionByParcelPosition(parcel);
            SceneEntityDefinition? sceneDef = await retrieveScene.ByParcelAsync(parcel, ct);

            if (sceneDef != null)

                // Override parcel as it's a new target
                parcel = sceneDef.metadata.scene.DecodedBase;

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            // Add report to the queue so it will be grabbed by the actual scene
            sceneReadinessReportQueue.Enqueue(parcel, loadReport);

            TeleportCharacterQuery(world, new PlayerTeleportIntent(characterPos, parcel, loadReport));

            if (sceneDef == null)
            {
                // Instant completion for empty parcels
                loadReport.SetProgress(1f);
                return;
            }

            try
            {
                // add timeout in case of a trouble
                await loadReport.CompletionSource.Task.Timeout(TimeSpan.FromSeconds(30));
            }
            catch (Exception e) { loadReport.CompletionSource.TrySetException(e); }
        }

        private SpawnPoint PickSpawnPoint(IReadOnlyList<SpawnPoint> spawnPoints, Vector3 targetWorldPosition, Vector3 parcelBaseWorldPosition)
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
            static float GetMidPoint(float[] coordArray)
            {
                var sum = 0f;

                for (var i = 0; i < coordArray.Length; i++)
                    sum += (int)coordArray[i];

                return sum / coordArray.Length;
            }

            static float? GetSpawnComponent(SpawnPoint.Coordinate coordinate)
            {
                if (coordinate.SingleValue != null)
                    return coordinate.SingleValue.Value;

                if (coordinate.MultiValue != null)
                    return GetMidPoint(coordinate.MultiValue);

                return null;
            }

            return new Vector3(
                GetSpawnComponent(spawnPoint.position.x) ?? ParcelMathHelper.PARCEL_SIZE / 2f,
                GetSpawnComponent(spawnPoint.position.y) ?? 0,
                GetSpawnComponent(spawnPoint.position.z) ?? ParcelMathHelper.PARCEL_SIZE / 2f);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void TeleportCharacter([Data] PlayerTeleportIntent intent, in Entity entity)
        {
            world?.Add(entity, intent);
        }

        [Query]
        [All(typeof(CameraComponent))]
        private void ForceCameraLookAt([Data] CameraLookAtIntent intent, in Entity entity)
        {
            world?.Add(entity, intent);
        }

        [Query]
        [All(typeof(CharacterRigidTransform), typeof(CharacterTransform))]
        private void ForceCharacterLookAt([Data] PlayerLookAtIntent intent, in Entity entity)
        {
            world?.Add(entity, intent);
        }
    }
}
