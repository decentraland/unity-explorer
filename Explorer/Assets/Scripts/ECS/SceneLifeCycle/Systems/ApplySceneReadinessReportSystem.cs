using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.CharacterMotion.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Applies the SceneReadinessReport to the corresponding scene entity
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))] // before the actual scene promises creation
    [UpdateBefore(typeof(ResolveStaticPointersSystem))] // before the actual scene promises creation
    // definitions are loaded in Presentation System Group so there is no gap between this system and promises creation
    public partial class ApplySceneReadinessReportSystem : BaseUnityLoopSystem
    {
        private readonly IScenesCache scenesCache;

        internal ApplySceneReadinessReportSystem(World world, IScenesCache scenesCache) : base(world)
        {
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            // Tolerate performance as it's not supposed to be called often

            GetReportFromTeleportIntentionQuery(World);
            MergeReportWithSceneEntityQuery(World);
        }

        private bool TryResolveReadinessReport(SceneReadinessReport sceneReadinessReport, Vector2Int parcel)
        {
            if (scenesCache.Contains(parcel))
            {
                sceneReadinessReport.CompletionSource.TrySetResult();
                return true;
            }

            var found = false;
            FindSceneForReportQuery(World, (sceneReadinessReport, parcel), ref found);
            return found;
        }

        [Query]
        private void GetReportFromTeleportIntention(PlayerTeleportIntent teleportIntent)
        {
            if (teleportIntent.SceneReadyReport != null)
                TryResolveReadinessReport(teleportIntent.SceneReadyReport, teleportIntent.Parcel);
        }

        /// <summary>
        ///     Common query if scene readiness report is added as a separate entity
        /// </summary>
        [Query]
        private void MergeReportWithSceneEntity(in Entity entity, ref SceneReadinessReport sceneReadinessReport, in Vector2Int parcel)
        {
            // If scene is already loaded just fire the completion event
            if (TryResolveReadinessReport(sceneReadinessReport, parcel))
                World.Destroy(entity);
        }

        [Query]
        [None(typeof(ISceneFacade), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]

        // TODO if asset promise is already created SceneReadinessReport won't be propagated to the scene factory
        private void FindSceneForReport([Data] (SceneReadinessReport sceneReadinessReport, Vector2Int parcel) input, [Data] ref bool found, Entity entity, in SceneDefinitionComponent definition)
        {
            if (found) return;

            for (var i = 0; i < definition.Parcels.Count; i++)
            {
                Vector2Int sceneParcel = definition.Parcels[i];

                if (sceneParcel == input.parcel)
                {
                    found = true;
                    World.Add(entity, input.sceneReadinessReport);
                    return;
                }
            }
        }

        // TODO handle scene definition loading failure, it should result in CompletionSource.TrySetException()
    }
}
