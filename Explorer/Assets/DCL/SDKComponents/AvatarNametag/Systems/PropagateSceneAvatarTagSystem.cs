using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.ColorComponent;
using SceneRunner.Scene;
using Utility.Arch;

namespace DCL.SDKComponents.AvatarNametag.Systems
{
    /// <summary>
    ///     Turns the scene-authored <see cref="PBAvatarNametag" /> into the global-world
    ///     <see cref="SceneAvatarTagComponent" /> that draws the plate above a player's nametag.
    ///     Only player entities resolve to an avatar, which is what keeps V1 to players: a scene-spawned
    ///     avatar carries neither the local player's CRDT id nor an <see cref="SDKProfile" />, so it is a no-op.
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

            // An empty label is the documented signal for "no plate", and needs no target of its own.
            if (string.IsNullOrEmpty(pbNametag.Label))
            {
                pbNametag.IsDirty = false;

                if (hasPlate)
                {
                    World.Remove<SceneAvatarTagApplied>(entity);
                    MarkPlateRemoving(applied.GlobalEntity);
                }

                return;
            }

            // Leave IsDirty set so the write is retried once the avatar behind this entity exists.
            if (!TryResolveTarget(entity, in crdtEntity, out Entity target))
                return;

            // Remote-player entity ids are recycled, so a re-resolve can land on a different avatar
            // than the one currently wearing this scene entity's plate.
            if (hasPlate && applied.GlobalEntity != target)
                MarkPlateRemoving(applied.GlobalEntity);

            globalWorld.AddOrSet(target, new SceneAvatarTagComponent(
                pbNametag.Label,
                pbNametag.LabelColor?.ToUnityColor() ?? SceneAvatarTagComponent.NATIVE_TEXT_COLOR,
                pbNametag.BackgroundColor?.ToUnityColor() ?? SceneAvatarTagComponent.NATIVE_BACKGROUND_COLOR));

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

            target = Entity.Null;

            // A remote player's wallet sits on the very same scene entity, which keeps the lookup O(1)
            // instead of scanning the global world for an avatar with a matching id.
            if (!World.TryGet(sceneEntity, out SDKProfile? sdkProfile))
                return false;

            string? userId = sdkProfile?.UserId;

            if (string.IsNullOrEmpty(userId)
                || !entityParticipantTable.TryGet(userId, out IReadOnlyEntityParticipantTable.Entry entry)
                || !globalWorld.IsAlive(entry.Entity))
                return false;

            target = entry.Entity;
            return true;
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
