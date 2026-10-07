using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using Google.Protobuf.Collections;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;

namespace DCL.SDKComponents.SceneWearables.Systems
{
    /// <summary>
    ///     Copies this scene's own scene wearables from the <see cref="PBAvatarShape" /> on <see cref="SpecialEntitiesID.PLAYER_ENTITY" />
    ///     into the local player's <see cref="SceneWearablesOverride" /> while the scene is current.
    /// </summary>
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(ResolveSceneWearablePathsSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    [ThrottlingEnabled]
    public partial class PropagateSceneWearablesToPlayerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;
        private readonly string sceneWearableUrnPrefix;

        internal PropagateSceneWearablesToPlayerSystem(World world, ISceneData sceneData, ISceneStateProvider sceneStateProvider, World globalWorld, Entity globalPlayerEntity) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            sceneWearableUrnPrefix = SceneWearableDTOFactory.GetUrnPrefix(sceneData.SceneEntityDefinition.id);
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            if (isCurrent)
                MarkPlayerAvatarShapeDirtyQuery(World);
            else
                ClearAppliedOverrideQuery(World);
        }

        public void FinalizeComponents(in Query query) =>
            ClearAppliedOverrideQuery(World);

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            HandleComponentRemovedQuery(World);
            HandleEntityDestructionQuery(World);
            PropagateOverrideQuery(World);
        }

        [Query]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        [All(typeof(SceneWearablesOverrideApplied))]
        private void HandleComponentRemoved(Entity entity) =>
            ClearOverride(entity);

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(SceneWearablesOverrideApplied))]
        private void HandleEntityDestruction(Entity entity) =>
            ClearOverride(entity);

        [Query]
        [All(typeof(SceneWearablesOverrideApplied))]
        private void ClearAppliedOverride(Entity entity) =>
            ClearOverride(entity);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateOverride(Entity entity, in CRDTEntity crdtEntity, in PBAvatarShape pbAvatarShape)
        {
            if (!pbAvatarShape.IsDirty || crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY) return;

            ref SceneWearablesOverride sceneWearables = ref globalWorld.Get<SceneWearablesOverride>(globalPlayerEntity);

            if (TryCopySceneWearables(pbAvatarShape.Wearables, sceneWearables.Urns))
                sceneWearables.IsDirty = true;

            World.AddOrGet<SceneWearablesOverrideApplied>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void MarkPlayerAvatarShapeDirty(in CRDTEntity crdtEntity, in PBAvatarShape pbAvatarShape)
        {
            if (crdtEntity.Id == SpecialEntitiesID.PLAYER_ENTITY)
                pbAvatarShape.IsDirty = true;
        }

        private void ClearOverride(Entity entity)
        {
            ref SceneWearablesOverride sceneWearables = ref globalWorld.Get<SceneWearablesOverride>(globalPlayerEntity);

            if (sceneWearables.Urns.Count > 0)
            {
                sceneWearables.Urns.Clear();
                sceneWearables.IsDirty = true;
            }

            World.Remove<SceneWearablesOverrideApplied>(entity);
        }

        private bool TryCopySceneWearables(RepeatedField<string> wearables, List<URN> target)
        {
            var sceneWearablesCount = 0;
            var changed = false;

            for (var i = 0; i < wearables.Count; i++)
            {
                string wearable = wearables[i];

                if (!IsOwnSceneWearable(wearable)) continue;

                if (sceneWearablesCount >= target.Count || !string.Equals(target[sceneWearablesCount].ToString(), wearable, StringComparison.OrdinalIgnoreCase))
                    changed = true;

                sceneWearablesCount++;
            }

            if (!changed && sceneWearablesCount == target.Count)
                return false;

            target.Clear();

            for (var i = 0; i < wearables.Count; i++)
                if (IsOwnSceneWearable(wearables[i]))
                    target.Add(new URN(wearables[i]));

            return true;
        }

        private bool IsOwnSceneWearable(string pointer) =>
            pointer.StartsWith(sceneWearableUrnPrefix, StringComparison.OrdinalIgnoreCase);

        private struct SceneWearablesOverrideApplied { }
    }
}
