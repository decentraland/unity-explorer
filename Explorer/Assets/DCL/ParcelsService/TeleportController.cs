using Arch.Core;
using Arch.System;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Character.Components;
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
                AddTeleportIntentQuery(world, new PlayerTeleportIntent(ParcelMathHelper.GetPositionByParcelPosition(parcel), parcel));
                loadReport.ProgressCounter.Value = 1f;
                loadReport.CompletionSource.TrySetResult();
                return null;
            }

            SceneEntityDefinition sceneDef = await retrieveScene.ByParcelAsync(parcel, ct);

            Vector3 targetPosition;

            if (sceneDef != null)
            {
                // Override parcel as it's a new target
                parcel = sceneDef.metadata.scene.DecodedBase;

                targetPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel);

                List<SceneMetadata.SpawnPoint> spawnPoints = sceneDef.metadata.spawnPoints;

                if (spawnPoints is { Count: > 0 })
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

                    Vector3 offset = GetOffsetFromSpawnPoint(spawnPoint);

                    // TODO validate offset position is within bounds of one of scene parcels

                    targetPosition += offset;
                }
            }
            else
                targetPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel);

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            AddTeleportIntentQuery(retrieveScene.World, new PlayerTeleportIntent(targetPosition, parcel));

            if (sceneDef == null)
            {
                // Instant completion for empty parcels
                loadReport.ProgressCounter.Value = 1;
                loadReport.CompletionSource.TrySetResult();

                return null;
            }

            return new WaitForSceneReadiness(parcel, loadReport, sceneReadinessReportQueue);
        }

        public async UniTask TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            if (retrieveScene == null)
            {
                AddTeleportIntentQuery(world, new PlayerTeleportIntent(ParcelMathHelper.GetPositionByParcelPosition(parcel), parcel));
                loadReport.ProgressCounter.Value = 1f;
                loadReport.CompletionSource.TrySetResult();
                return;
            }

            Vector3 characterPos = ParcelMathHelper.GetPositionByParcelPosition(parcel);
            SceneEntityDefinition sceneDef = await retrieveScene.ByParcelAsync(parcel, ct);

            if (sceneDef != null)

                // Override parcel as it's a new target
                parcel = sceneDef.metadata.scene.DecodedBase;

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            // Add report to the queue so it will be grabbed by the actual scene
            sceneReadinessReportQueue.Enqueue(parcel, loadReport);

            AddTeleportIntentQuery(world, new PlayerTeleportIntent(characterPos, parcel));

            if (sceneDef == null)
            {
                // Instant completion for empty parcels
                loadReport.ProgressCounter.Value = 1;
                loadReport.CompletionSource.TrySetResult();

                return;
            }

            try
            {
                // add timeout in case of a trouble
                await loadReport.CompletionSource.Task.Timeout(TimeSpan.FromSeconds(30));
            }
            catch (Exception e) { loadReport.CompletionSource.TrySetException(e); }
        }

        private static Vector3 GetOffsetFromSpawnPoint(SceneMetadata.SpawnPoint spawnPoint)
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

        [Query]
        [All(typeof(PlayerComponent))]
        private void AddTeleportIntent([Data] PlayerTeleportIntent intent, in Entity entity)
        {
            world?.Add(entity, intent);
        }
    }
}
