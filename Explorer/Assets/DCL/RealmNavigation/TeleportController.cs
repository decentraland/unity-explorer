using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation
{
    public class TeleportController : ITeleportController
    {
        private static readonly QueryDescription BANNED_SCENES_QUERY =
            new QueryDescription().WithAll<SceneDefinitionComponent, BannedSceneComponent>();

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

        public void StartTeleportToSpawnPoint(SceneEntityDefinition sceneDataSceneEntityDefinition, CancellationToken ct) =>
            world?.AddOrGet(playerEntity, new PlayerTeleportIntent(sceneDataSceneEntityDefinition, Vector2Int.zero, TeleportUtils.PickTargetWithOffset(sceneDataSceneEntityDefinition, sceneDataSceneEntityDefinition.metadata.scene.DecodedBase).targetWorldPosition, ct, isPositionSet: true));

        private async UniTask<WaitForSceneReadiness?> TeleportAsync(Vector2Int parcel, AsyncLoadProcessReport loadReport, CancellationToken ct, bool nullifySceneDef = false)
        {
            if (retrieveScene == null)
            {
                world?.AddOrGet(playerEntity, new PlayerTeleportIntent(null, parcel, Vector3.zero, ct, loadReport));
                loadReport.SetProgress(1f);
                return null;
            }

            SceneEntityDefinition? sceneDef = await retrieveScene.ByParcelAsync(parcel, ct);

            if (sceneDef != null && !TeleportUtils.IsRoad(sceneDef.metadata.OriginalJson.AsSpan()))
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

            // Banned destination: the scene has been disposed and won't be reloaded, so no system will ever
            // dequeue the readiness report. Complete it now so the loading screen closes and the avatar lands
            // at the requested position with the scene unloaded (same UX as cross-realm entry into a banned world).
            if (IsSceneBanned(sceneDef.id))
            {
                loadReport.SetProgress(1f);
                return null;
            }

            return new WaitForSceneReadiness(parcel, loadReport, sceneReadinessReportQueue);
        }

        private bool IsSceneBanned(string? sceneId)
        {
            if (world == null || string.IsNullOrEmpty(sceneId)) return false;

            // Chunk iteration to avoid the delegate/closure allocation of World.Query(ForEach).
            // The matched archetype is empty in the common case (no current bans), so iteration is effectively free.
            foreach (ref Chunk chunk in world.Query(in BANNED_SCENES_QUERY).GetChunkIterator())
            {
                ref SceneDefinitionComponent first = ref chunk.GetFirst<SceneDefinitionComponent>();

                foreach (int i in chunk)
                {
                    ref SceneDefinitionComponent definition = ref Unsafe.Add(ref first, i);
                    if (definition.Definition.id == sceneId)
                        return true;
                }
            }

            return false;
        }
    }
}
