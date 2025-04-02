using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using DCL.Utilities;
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
        /// If current scene is still loading it will block the teleport until its assets are resolved or timed out
        /// </summary>
        public UniTask<WaitForSceneReadiness?> TeleportToSceneSpawnPointAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct) =>
            TeleportAsync(parcel, loadReport, false, ct);
            // TeleportAsync(parcel, TeleportationUtils.PickTargetWithOffset, loadReport, ct);

        /// <summary>
        /// Debug teleportation
        /// </summary>
        public UniTask TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct) =>
            TeleportAsync(parcel, loadReport, true, ct);
            // TeleportAsync(parcel, TeleportationUtils.PickTarget, loadReport, ct);

            private async UniTask<WaitForSceneReadiness?> TeleportAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, bool isFromDebugWindow, CancellationToken ct)
            {
                if (retrieveScene == null)
                {
                    world?.AddOrGet(playerEntity, new PlayerTeleportIntent(null, parcel, null, ct, loadReport));
                    loadReport.SetProgress(1f);
                    return null;
                }

                SceneEntityDefinition? sceneDef = await retrieveScene.ByParcelAsync(parcel, ct);

                if (sceneDef != null)
                    if(isFromDebugWindow || !TeleportationUtils.IsTramLine(sceneDef.metadata.OriginalJson.AsSpan()))
                        parcel = sceneDef.metadata.scene.DecodedBase; // Override parcel as it's a new target

                if (isFromDebugWindow)
                    sceneDef = null;


            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);

            world?.AddOrGet(playerEntity, new PlayerTeleportIntent(sceneDef, parcel, position: null, ct, loadReport));

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
