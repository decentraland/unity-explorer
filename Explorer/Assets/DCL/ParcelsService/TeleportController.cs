using Arch.Core;
using Arch.System;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using ECS.SceneLifeCycle.Reporting;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.ParcelsService
{
    public class TeleportController : ITeleportController
    {
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
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
                TeleportCharacter(new PlayerTeleportIntent(ParcelMathHelper.GetPositionByParcelPosition(parcel, true), parcel, loadReport));
                loadReport.SetProgress(1f);
                return null;
            }

            SceneEntityDefinition? sceneDef = await retrieveScene.ByParcelAsync(parcel, ct);

            Vector3 targetPosition;
            Vector3? cameraTarget = null;

            if (sceneDef != null)
            {
                // Override parcel as it's a new target
                parcel = sceneDef.metadata.scene.DecodedBase;

                targetPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel);

                List<SceneMetadata.SpawnPoint>? spawnPoints = sceneDef.metadata.spawnPoints;

                if (spawnPoints is { Count: > 0 })
                {
                    SceneMetadata.SpawnPoint spawnPoint = PickSpawnPoint(spawnPoints);

                    // TODO validate offset position is within bounds of one of scene parcels
                    targetPosition += GetSpawnPositionOffset(spawnPoint);

                    if (spawnPoint.cameraTarget != null)
                        cameraTarget = spawnPoint.cameraTarget!.Value.ToVector3() + GetSpawnCameraOffset(sceneDef);
                }
            }
            else
                targetPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel, true);

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            TeleportCharacter(new PlayerTeleportIntent(targetPosition, parcel, loadReport));

            if (cameraTarget != null)
            {
                ForceCameraLookAt(new CameraLookAtIntent(cameraTarget.Value, targetPosition));
                ForceCharacterLookAt(new PlayerLookAtIntent(cameraTarget.Value, targetPosition));
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
                TeleportCharacter(new PlayerTeleportIntent(ParcelMathHelper.GetPositionByParcelPosition(parcel, true), parcel, loadReport));
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

            TeleportCharacter(new PlayerTeleportIntent(characterPos, parcel, loadReport));

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

        private SceneMetadata.SpawnPoint PickSpawnPoint(IReadOnlyList<SceneMetadata.SpawnPoint> spawnPoints)
        {
            // TODO transfer obscure logic of how to pick the desired spawn point from the array
            // For now just pick default/first
            SceneMetadata.SpawnPoint spawnPoint = spawnPoints[0];

            for (var i = 0; i < spawnPoints.Count; i++)
            {
                SceneMetadata.SpawnPoint sp = spawnPoints[i];
                if (!sp.@default) continue;

                spawnPoint = sp;
                break;
            }

            return spawnPoint;
        }

        private Vector3 GetSpawnCameraOffset(SceneEntityDefinition sceneDef)
        {
            Vector2 baseParcel = sceneDef.metadata.scene.DecodedBase;
            return new Vector3(baseParcel.x * ParcelMathHelper.PARCEL_SIZE, 0, baseParcel.y * ParcelMathHelper.PARCEL_SIZE);
        }

        private static Vector3 GetSpawnPositionOffset(SceneMetadata.SpawnPoint spawnPoint)
        {
            static float GetMidPoint(float[] coordArray)
            {
                var sum = 0f;

                for (var i = 0; i < coordArray.Length; i++)
                    sum += (int)coordArray[i];

                return sum / coordArray.Length;
            }

            static float? GetSpawnComponent(SceneMetadata.SpawnPoint.Coordinate coordinate)
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
