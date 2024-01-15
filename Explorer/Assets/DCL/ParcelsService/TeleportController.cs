using Arch.Core;
using Arch.System;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.SceneLoadingScreens;
using DCL.SceneReadiness;
using Ipfs;
using MVC;
using SceneRunner.Scene;
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
        private readonly MVCManager mvcManager;
        private IRetrieveScene retrieveScene;
        private World world;

        public TeleportController(ISceneReadinessReportQueue sceneReadinessReportQueue,
            MVCManager mvcManager)
        {
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.mvcManager = mvcManager;
        }

        public void InvalidateRealm()
        {
            retrieveScene = null;
        }

        public void OnWorldLoaded(World world)
        {
            this.world = world;
        }

        public void OnRealmLoaded(IRetrieveScene retrieveScene)
        {
            this.retrieveScene = retrieveScene;
            this.retrieveScene.World = world;
        }

        public async UniTask TeleportToSceneSpawnPointAsync(Vector2Int parcel, CancellationToken ct)
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

                if (spawnPoints.Count > 0)
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

            var readinessCompletion = new SceneReadinessReport(new UniTaskCompletionSource());

            // Add report to the queue so it will be grabbed by the actual scene
            sceneReadinessReportQueue.Enqueue(parcel, readinessCompletion);

            AddTeleportIntentQuery(retrieveScene.World, new PlayerTeleportIntent(targetPosition, parcel, readinessCompletion));

            mvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(parcel, readinessCompletion)))
                      .Forget();

            // add timeout in case of a trouble
            await readinessCompletion.CompletionSource.Task.Timeout(TimeSpan.FromSeconds(30));

            // TODO Hide Loading Screen
            // TODO Handle Timeout and other exception
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
            AddTeleportIntentQuery(world, new PlayerTeleportIntent(characterPos, parcel, null));
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void AddTeleportIntent([Data] PlayerTeleportIntent intent, in Entity entity)
        {
            world.Add(entity, intent);
        }
    }
}
