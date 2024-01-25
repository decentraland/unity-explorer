using Arch.Core;
using Arch.System;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using ECS.SceneLifeCycle.Reporting;
using Ipfs;
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

        public async UniTask TeleportToSceneSpawnPointAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            // If type of retrieval is not set yet
            if (retrieveScene == null)
            {
                TeleportToParcel(parcel);
                return;
            }

            IpfsTypes.SceneEntityDefinition sceneDef = await retrieveScene.ByParcelAsync(parcel, ct);

            Vector3 targetPosition;

            if (sceneDef != null)
            {
                // Override parcel as it's a new target
                parcel = sceneDef.metadata.scene.DecodedBase;

                targetPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel);

                List<IpfsTypes.SceneMetadata.SpawnPoint> spawnPoints = sceneDef.metadata.spawnPoints;

                if (spawnPoints is { Count: > 0 })
                {
                    // TODO transfer obscure logic of how to pick the desired spawn point from the array
                    // For now just pick default/first

                    IpfsTypes.SceneMetadata.SpawnPoint spawnPoint = spawnPoints[0];

                    for (var i = 0; i < spawnPoints.Count; i++)
                    {
                        IpfsTypes.SceneMetadata.SpawnPoint sp = spawnPoints[i];
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

                return;
            }

            // Add report to the queue so it will be grabbed by the actual scene
            sceneReadinessReportQueue.Enqueue(parcel, loadReport);

            try
            {
                // add timeout in case of a trouble
                await loadReport.CompletionSource.Task.Timeout(TimeSpan.FromSeconds(30));
            }
            catch (Exception e) { loadReport.CompletionSource.TrySetException(e); }
        }

        private static Vector3 GetOffsetFromSpawnPoint(IpfsTypes.SceneMetadata.SpawnPoint spawnPoint)
        {
            if (spawnPoint.SP != null)
            {
                IpfsTypes.SceneMetadata.SpawnPoint.SinglePosition val = spawnPoint.SP.Value;
                return new Vector3(val.x, val.y, val.z);
            }

            if (spawnPoint.MP != null)
            {
                static float GetMidPoint(float[] coordArray)
                {
                    var sum = 0f;

                    for (var i = 0; i < coordArray.Length; i++)
                        sum += (int)coordArray[i];

                    return sum / coordArray.Length;
                }

                IpfsTypes.SceneMetadata.SpawnPoint.MultiPosition val = spawnPoint.MP.Value;
                return new Vector3(GetMidPoint(val.x), GetMidPoint(val.y), GetMidPoint(val.z));
            }

            // Center
            return new Vector3(ParcelMathHelper.PARCEL_SIZE / 2f, 0, ParcelMathHelper.PARCEL_SIZE / 2f);
        }

        public void TeleportToParcel(Vector2Int parcel)
        {
            Vector3 characterPos = ParcelMathHelper.GetPositionByParcelPosition(parcel);
            AddTeleportIntentQuery(world, new PlayerTeleportIntent(characterPos, parcel));
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void AddTeleportIntent([Data] PlayerTeleportIntent intent, in Entity entity)
        {
            world?.Add(entity, intent);
        }
    }
}
