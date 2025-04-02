using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using DCL.Landscape;
using DCL.Utilities;
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

namespace DCL.RealmNavigation
{
    public class TeleportController : ITeleportController
    {
        private delegate void PickTargetDelegate(SceneEntityDefinition? sceneDef, ref Vector2Int parcel, out Vector3 targetWorldPosition, out Vector3? cameraTarget);

        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private TerrainGenerator terrain;

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

        public void SetTerrain(TerrainGenerator terrain)
        {
            this.terrain = terrain;
        }

        /// <summary>
        /// If current scene is still loading it will block the teleport until its assets are resolved or timed out
        /// </summary>
        public UniTask<WaitForSceneReadiness?> TeleportToSceneSpawnPointAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct) =>
            TeleportAsync(parcel, TeleportationUtils.PickTargetWithOffset, loadReport, ct);

        /// <summary>
        /// Debug teleportation
        /// </summary>
        public UniTask TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct) =>
            TeleportAsync(parcel, TeleportationUtils.PickTarget, loadReport, ct);

        private async UniTask<WaitForSceneReadiness?> TeleportAsync(Vector2Int parcel, PickTargetDelegate pickTargetDelegate,
            AsyncLoadProcessReport loadReport, CancellationToken ct)
        {
            terrain.SetTerrainCollider(parcel, true);

            if (retrieveScene == null)
            {
                var position = ParcelMathHelper.GetPositionByParcelPosition(parcel).WithTerrainOffset();

                world?.AddOrGet(playerEntity, new PlayerTeleportIntent(position, parcel, ct, loadReport));

                loadReport.SetProgress(1f);
                return null;
            }

            SceneEntityDefinition? sceneDef = await retrieveScene.ByParcelAsync(parcel, ct);

            pickTargetDelegate(sceneDef, ref parcel, out Vector3 targetWorldPosition, out Vector3? cameraTarget);

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            PlayerTeleportIntent Intent2 = new PlayerTeleportIntent(targetWorldPosition, parcel, ct, loadReport);
            world?.AddOrGet(playerEntity, Intent2);

            if (cameraTarget != null)
            {
                world?.AddOrGet(cameraEntity, new CameraLookAtIntent(cameraTarget.Value, targetWorldPosition));
                world?.AddOrGet(playerEntity, new PlayerLookAtIntent(cameraTarget.Value, targetWorldPosition));
            }

            if (sceneDef == null)
            {
                // Instant completion for empty parcels
                loadReport.SetProgress(1f);

                return null;
            }

            return new WaitForSceneReadiness(parcel, loadReport, sceneReadinessReportQueue);
        }
    }
}
