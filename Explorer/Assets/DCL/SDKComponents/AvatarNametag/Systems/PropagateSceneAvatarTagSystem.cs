using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Multiplayer.SDK.Components;
using DCL.Nametags;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.ColorComponent;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Arch;
using static DCL.Nametags.SceneAvatarTagComponent;

namespace DCL.SDKComponents.AvatarNametag.Systems
{
    /// <summary>
    ///     Turns the scene-authored <see cref="PBAvatarNametag" /> into the global-world
    ///     <see cref="SceneAvatarTagComponent" /> that draws the plate above an avatar's nametag.
    ///     A target resolves for the local player (by CRDT id), a remote player (by <see cref="SDKProfile" />
    ///     wallet) or a scene-spawned avatar (by its <see cref="SDKAvatarShapeComponent" /> global-world twin);
    ///     on any other entity the component is a no-op.
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class PropagateSceneAvatarTagSystem : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;

        internal PropagateSceneAvatarTagSystem(World world, ISceneStateProvider sceneStateProvider,
            IReadOnlyEntityParticipantTable entityParticipantTable, World globalWorld, Entity globalPlayerEntity) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.entityParticipantTable = entityParticipantTable;
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            if (isCurrent)
            {
                // The plates were dropped on exit, so every write has to be replayed on re-entry.
                MarkNametagsDirtyQuery(World);
                return;
            }

            DropNametagQuery(World);
        }

        public void FinalizeComponents(in Query query) =>
            DropNametagQuery(World);

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            HandleComponentRemovedQuery(World);
            HandleEntityDestructionQuery(World);
            PropagateNametagQuery(World);
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
        private void DropNametag(Entity entity, in SceneAvatarTagApplied applied)
        {
            Entity target = applied.GlobalEntity;

            World.Remove<SceneAvatarTagApplied>(entity);
            MarkPlateRemoving(target);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateNametag(Entity entity, in CRDTEntity crdtEntity, in PBAvatarNametag pbNametag)
        {
            if (!pbNametag.IsDirty) return;

            bool hasPlate = World.TryGet(entity, out SceneAvatarTagApplied applied);

            // Leave IsDirty set so the write is retried once the avatar behind this entity exists.
            if (!TryResolveTarget(entity, in crdtEntity, out Entity target))
                return;

            // Remote-player entity ids are recycled, so a re-resolve can land on a different avatar
            // than the one currently wearing this scene entity's plate.
            if (hasPlate && applied.GlobalEntity != target)
                MarkPlateRemoving(applied.GlobalEntity);

            Color backgroundColor = pbNametag.BackgroundColor.ToUnityColor(fallback: NATIVE_BACKGROUND_COLOR);

            globalWorld.AddOrSet(target, new SceneAvatarTagComponent(
                // An empty label draws the bare plate instead of hiding it, so a scene can color-code
                // players without labelling them; the plate goes away with the component.
                pbNametag.Label,
                pbNametag.LabelColor.ToUnityColor(fallback: NATIVE_TEXT_COLOR),
                backgroundColor,
                // A border the scene did not ask for takes the background color, leaving the plate
                // a flat capsule - the rim is opt-in rather than derived from the background.
                pbNametag.BorderColor.ToUnityColor(fallback: backgroundColor)));

            pbNametag.IsDirty = false;

            // Structural change last: it moves the entity between archetypes and invalidates the
            // component references this query is holding.
            World.AddOrSet(entity, new SceneAvatarTagApplied(target));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void MarkNametagsDirty(ref PBAvatarNametag pbNametag) =>
            pbNametag.IsDirty = true;

        private bool TryResolveTarget(Entity sceneEntity, in CRDTEntity crdtEntity, out Entity target)
        {
            if (crdtEntity.Id == SpecialEntitiesID.PLAYER_ENTITY)
            {
                target = globalPlayerEntity;
                return globalWorld.IsAlive(target);
            }

            // A scene-spawned avatar keeps a handle to its global-world twin on the very same scene entity.
            if (World.TryGet(sceneEntity, out SDKAvatarShapeComponent sdkAvatarShape))
            {
                target = sdkAvatarShape.GlobalWorldEntity;
                return globalWorld.IsAlive(target);
            }

            target = Entity.Null;

            string userId = string.Empty;

            // The scene's write materializes on the CRDT bridge's own entity, while the multiplayer
            // bridge keeps the player's SDKProfile on a separate scene entity that carries no CRDTEntity
            // at all - the two representations of one player share nothing but the CRDT id, so when the
            // profile is not on the write's entity it is found through that id.
            if (World.TryGet(sceneEntity, out SDKProfile? sdkProfile))
                userId = sdkProfile?.UserId ?? string.Empty;
            else
                FindWalletByCrdtIdQuery(World, in crdtEntity, ref userId);

            if (string.IsNullOrEmpty(userId)
                || !entityParticipantTable.TryGet(userId, out IReadOnlyEntityParticipantTable.Entry entry)
                || !globalWorld.IsAlive(entry.Entity))
                return false;

            target = entry.Entity;
            return true;
        }

        [Query]
        private void FindWalletByCrdtId([Data] in CRDTEntity searchedId, [Data] ref string wallet, in PlayerSceneCRDTEntity playerCrdtEntity, in SDKProfile profile)
        {
            if (playerCrdtEntity.CRDTEntity.Id == searchedId.Id)
                wallet = profile.UserId;
        }

        private void MarkPlateRemoving(Entity target)
        {
            if (!globalWorld.IsAlive(target)) return;

            ref SceneAvatarTagComponent plate = ref globalWorld.TryGetRef<SceneAvatarTagComponent>(target, out bool exists);

            // NametagPlacementSystem hides the plate first and removes the component afterwards,
            // so flag it instead of removing it from here.
            if (exists)
                plate.IsRemoving = true;
        }

        /// <summary>
        ///     Remembers which global-world avatar this scene entity's plate landed on, so that the plate can be
        ///     dropped later without resolving a wallet that may already have been released.
        /// </summary>
        private struct SceneAvatarTagApplied
        {
            public readonly Entity GlobalEntity;

            public SceneAvatarTagApplied(Entity globalEntity)
            {
                GlobalEntity = globalEntity;
            }
        }
    }
}
