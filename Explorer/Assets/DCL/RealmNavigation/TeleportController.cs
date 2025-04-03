using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation
{
    public class TeleportController : ITeleportController
    {
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;

        private IRetrieveScene? retrieveScene;
        private World? world;
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

        /// <summary>
        ///     If current scene is still loading it will block the teleport until its assets are resolved or timed out
        /// </summary>
        public UniTask<WaitForSceneReadiness?> TeleportToSceneSpawnPointAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct) =>
            TeleportAsync(parcel, loadReport, ct);

        /// <summary>
        ///     Debug Widget teleportation
        /// </summary>
        public UniTask TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct) =>
            TeleportAsync(parcel, loadReport, ct, nullifySceneDef: true);

        private async UniTask<WaitForSceneReadiness?> TeleportAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct, bool nullifySceneDef = false)
        {
            if (retrieveScene == null)
            {
                world?.AddOrGet(playerEntity, new PlayerTeleportIntent(null, parcel, Vector3.zero, ct, loadReport));
                loadReport.SetProgress(1f);
                return null;
            }

            SceneEntityDefinition? sceneDef = await retrieveScene.ByParcelAsync(parcel, ct);

            if (sceneDef != null && !TeleportUtils.IsTramLine(sceneDef.metadata.OriginalJson.AsSpan()))
            {
                parcel = sceneDef.metadata.scene.DecodedBase; // Override parcel as it's a new target

                if (nullifySceneDef)
                    sceneDef = null;
            }

            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);

            world?.AddOrGet(playerEntity, new PlayerTeleportIntent(sceneDef, parcel, Vector3.zero, ct, loadReport));

            if (sceneDef == null)
            {
                loadReport.SetProgress(1f); // Almost instant completion for empty parcels
                return null;
            }

            return new WaitForSceneReadiness(parcel, loadReport, sceneReadinessReportQueue);
        }
    }
}
