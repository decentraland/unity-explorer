using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Nametags;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.ColorComponent;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Utility.Arch;
using static DCL.Nametags.SceneAvatarTagComponent;

namespace DCL.SDKComponents.AvatarNametag.Systems
{
    /// <summary>
    ///     Turns the scene-authored <see cref="PBAvatarNametag" /> into
    ///     the global-world  <see cref="SceneAvatarTagComponent" />
    ///     that draws the plate above an avatar's nametag.
    ///     A target resolves for:
    ///     <list type="bullet">
    ///         <item>a player inside the scene — by the <see cref="PlayerSceneCRDTEntity" /> sharing its CRDT id;</item>
    ///         <item>a scene-spawned avatar — by its <see cref="SDKAvatarShapeComponent" /> global-world twin.</item>
    ///     </list>
    ///     On any other entity the component is a no-op.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateBefore(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class PropagateSceneAvatarTagSystem : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;
        private readonly World globalWorld;

        internal PropagateSceneAvatarTagSystem(World world, ISceneStateProvider sceneStateProvider,
            IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, World globalWorld) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.entitiesMap = entitiesMap;
            this.globalWorld = globalWorld;
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            if (isCurrent)
                MarkNametagsDirtyQuery(World);
            else
                DropAllNametagsQuery(World);
        }

        public void FinalizeComponents(in Query query) =>
            DropAllNametagsQuery(World);

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            HandleComponentRemovedQuery(World);
            HandleEntityDestructionQuery(World);
            HandlePlayerLeftQuery(World);

            var dirtyPlayerNametags = 0;
            CountDirtyPlayerNametagsQuery(World, ref dirtyPlayerNametags);
            PropagatePlayerNametagQuery(World, dirtyPlayerNametags > 0);
            PropagateSceneAvatarNametagQuery(World);
        }

        [Query]
        [None(typeof(PBAvatarNametag), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoved(Entity entity, in SceneAvatarTagApplied applied) =>
            DropNametag(entity, in applied);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(Entity entity, in SceneAvatarTagApplied applied) =>
            DropNametag(entity, in applied);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandlePlayerLeft(Entity bridgeEntity, in PlayerSceneCRDTEntity player)
        {
            if (!entitiesMap.TryGetValue(player.CRDTEntity, out Entity entity)
                || !World.TryGet(entity, out SceneAvatarTagApplied applied)
                || applied.BridgeEntity != bridgeEntity)
                return;

            DropNametag(entity, in applied);
        }

        [Query]
        private void DropAllNametags(Entity entity, in SceneAvatarTagApplied applied) =>
            DropNametag(entity, in applied);

        private void DropNametag(Entity entity, in SceneAvatarTagApplied applied)
        {
            Entity target = applied.GlobalEntity;

            World.Remove<SceneAvatarTagApplied>(entity);
            MarkPlateRemoving(target);
        }

        // For a remote player the scene's PBAvatarNametag lives on a different entity than PlayerSceneCRDTEntity: the two share only the CRDT id.
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagatePlayerNametag([Data] bool anyPlayerNametagDirty, Entity bridgeEntity, in PlayerSceneCRDTEntity player)
        {
            if (!player.IsDirty && !anyPlayerNametagDirty) return;

            if (!entitiesMap.TryGetValue(player.CRDTEntity, out Entity entity)
                || !World.TryGet(entity, out PBAvatarNametag? pbNametag)
                || pbNametag == null)
                return;

            if ((!player.IsDirty && !pbNametag.IsDirty) || !globalWorld.IsAlive(player.GlobalEntity)) return;

            ApplyPlate(entity, pbNametag, player.GlobalEntity, bridgeEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CountDirtyPlayerNametags([Data] ref int count, in CRDTEntity crdtEntity, in PBAvatarNametag pbNametag)
        {
            if (pbNametag.IsDirty && SpecialEntitiesID.IsAnyPlayerEntity(crdtEntity))
                count++;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateSceneAvatarNametag(Entity entity, in CRDTEntity crdtEntity, in PBAvatarNametag pbNametag)
        {
            if (!pbNametag.IsDirty) return;

            // Still dirty after PropagatePlayerNametag: no such player is inside the scene, its arrival applies the plate.
            if (SpecialEntitiesID.IsAnyPlayerEntity(crdtEntity))
            {
                pbNametag.IsDirty = false;
                return;
            }

            // A scene-spawned avatar keeps a handle to its global-world twin on the very same scene entity.
            if (World.TryGet(entity, out SDKAvatarShapeComponent sdkAvatarShape))
            {
                if (globalWorld.IsAlive(sdkAvatarShape.GlobalWorldEntity))
                    ApplyPlate(entity, pbNametag, sdkAvatarShape.GlobalWorldEntity, Entity.Null);
                else
                    pbNametag.IsDirty = false;

                return;
            }

            // A shape without a twin is an avatar mid-creation, not an absent one: IsDirty stays set to retry.
            if (World.Has<PBAvatarShape>(entity)) return;

            pbNametag.IsDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void MarkNametagsDirty(ref PBAvatarNametag pbNametag) =>
            pbNametag.IsDirty = true;

        private void ApplyPlate(Entity entity, PBAvatarNametag pbNametag, Entity target, Entity bridgeEntity)
        {
            // Remote-player entity ids are recycled, so the id can land on a different avatar
            // than the one currently wearing this scene entity's plate.
            if (World.TryGet(entity, out SceneAvatarTagApplied applied) && applied.GlobalEntity != target)
                MarkPlateRemoving(applied.GlobalEntity);

            Color backgroundColor = pbNametag.BackgroundColor.ToUnityColor(fallback: NATIVE_BACKGROUND_COLOR);

            globalWorld.AddOrSet(target, new SceneAvatarTagComponent(
                pbNametag.Label,
                pbNametag.LabelColor.ToUnityColor(fallback: NATIVE_TEXT_COLOR),
                backgroundColor,
                pbNametag.BorderColor.ToUnityColor(fallback: backgroundColor)));

            pbNametag.IsDirty = false;

            // Structural change last: it invalidates the component references this query holds.
            World.AddOrSet(entity, new SceneAvatarTagApplied(target, bridgeEntity));
        }

        private void MarkPlateRemoving(Entity target)
        {
            if (!globalWorld.IsAlive(target)) return;

            ref SceneAvatarTagComponent plate = ref globalWorld.TryGetRef<SceneAvatarTagComponent>(target, out bool exists);

            if (exists)
                plate.IsRemoving = true;
        }

        /// <summary>
        ///     Remembers which global-world avatar this scene entity's plate landed on, so that the plate can be
        ///     dropped later without resolving the avatar again.
        /// </summary>
        private struct SceneAvatarTagApplied
        {
            public readonly Entity GlobalEntity;

            // A left player's bridge entity outlives the leave until its deletion runs, and may overlap with the bridge entity
            // of the player's re-entry: only the leave of the bridge entity that applied the plate may drop it.
            // Entity.Null for a scene-spawned avatar.
            public readonly Entity BridgeEntity;

            public SceneAvatarTagApplied(Entity globalEntity, Entity bridgeEntity)
            {
                GlobalEntity = globalEntity;
                BridgeEntity = bridgeEntity;
            }
        }
    }
}
