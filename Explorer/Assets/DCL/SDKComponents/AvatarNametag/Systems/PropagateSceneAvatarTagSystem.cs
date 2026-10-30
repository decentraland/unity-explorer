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
    ///         <item>the local player — by CRDT id;</item>
    ///         <item>a remote player — by <see cref="SDKProfile" /> wallet;</item>
    ///         <item>a scene-spawned avatar — by its <see cref="SDKAvatarShapeComponent" /> global-world twin.</item>
    ///     </list>
    ///     On any other entity the component is a no-op.
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class PropagateSceneAvatarTagSystem : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;
        private readonly List<PendingScan> pendingScans = new ();

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

            pendingScans.Clear();
            PropagateNametagQuery(World);

            if (pendingScans.Count == 0) return;

            MatchPendingPlayersQuery(World);
            ResolvePendingScans();
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
        private void DropAllNametags(Entity entity, in SceneAvatarTagApplied applied) =>
            DropNametag(entity, in applied);

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

            ResolveResult resolveResult = ResolveTarget(entity, in crdtEntity, out Entity target);

            // Ends the retry: nothing about this entity can turn it into an avatar anymore.
            if (resolveResult == ResolveResult.Unreachable)
            {
                pbNametag.IsDirty = false;
                return;
            }

            // Pending leaves IsDirty set, so the write is retried once the avatar shows up.
            if (resolveResult == ResolveResult.Pending)
                return;

            if (resolveResult == ResolveResult.NeedsPlayerScan)
            {
                pendingScans.Add(new PendingScan(entity, crdtEntity));
                return;
            }

            ApplyPlate(entity, pbNametag, target);
        }

        [Query]
        private void MatchPendingPlayers(Entity entity, in PlayerSceneCRDTEntity playerCrdtEntity)
        {
            for (var i = 0; i < pendingScans.Count; i++)
            {
                if (pendingScans[i].CrdtEntity.Id != playerCrdtEntity.CRDTEntity.Id) continue;

                PendingScan scan = pendingScans[i];
                scan.PlayerEntity = entity;
                pendingScans[i] = scan;
            }
        }

        internal void ResolvePendingScans()
        {
            for (var i = 0; i < pendingScans.Count; i++)
            {
                PendingScan scan = pendingScans[i];

                // ApplyPlate's structural changes relocate entities between entries, so the component
                // is re-fetched instead of carried over from the collecting query.
                if (!World.IsAlive(scan.SceneEntity) || !World.TryGet(scan.SceneEntity, out PBAvatarNametag? pbNametag) || pbNametag == null)
                    continue;

                // No player entity carries this id: it is a plain scene entity, or a player who left and
                // handed the reserved id back to the pool.
                if (scan.PlayerEntity == Entity.Null)
                {
                    pbNametag.IsDirty = false;
                    continue;
                }

                string userId = World.TryGet(scan.PlayerEntity, out SDKProfile? bridgeProfile)
                    ? bridgeProfile?.UserId ?? string.Empty
                    : string.Empty;

                ResolveResult resolveResult = ResolveByUserId(userId, out Entity target);

                if (resolveResult == ResolveResult.Unreachable)
                {
                    pbNametag.IsDirty = false;
                    continue;
                }

                if (resolveResult == ResolveResult.Pending)
                    continue;

                ApplyPlate(scan.SceneEntity, pbNametag, target);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void MarkNametagsDirty(ref PBAvatarNametag pbNametag) =>
            pbNametag.IsDirty = true;

        private ResolveResult ResolveTarget(Entity sceneEntity, in CRDTEntity crdtEntity, out Entity target)
        {
            target = Entity.Null;

            if (crdtEntity.Id == SpecialEntitiesID.PLAYER_ENTITY)
            {
                target = globalPlayerEntity;
                return globalWorld.IsAlive(target) ? ResolveResult.Resolved : ResolveResult.Unreachable;
            }

            // A scene-spawned avatar keeps a handle to its global-world twin on the very same scene entity.
            if (World.TryGet(sceneEntity, out SDKAvatarShapeComponent sdkAvatarShape))
            {
                target = sdkAvatarShape.GlobalWorldEntity;
                return globalWorld.IsAlive(target) ? ResolveResult.Resolved : ResolveResult.Unreachable;
            }

            // A shape without a twin is an avatar mid-creation, not an absent one.
            if (World.Has<PBAvatarShape>(sceneEntity))
                return ResolveResult.Pending;

            // One player has two scene entities that share nothing but the CRDT id: the CRDT bridge's,
            // which the scene writes to, and the multiplayer bridge's, which holds the SDKProfile.
            if (World.TryGet(sceneEntity, out SDKProfile? ownProfile))
                return ResolveByUserId(ownProfile?.UserId ?? string.Empty, out target);

            return ResolveResult.NeedsPlayerScan;
        }

        private ResolveResult ResolveByUserId(string userId, out Entity target)
        {
            target = Entity.Null;

            // A player entity without a profile is mid-setup, not gone.
            if (string.IsNullOrEmpty(userId))
                return ResolveResult.Pending;

            if (!entityParticipantTable.TryGet(userId, out IReadOnlyEntityParticipantTable.Entry entry)
                || !globalWorld.IsAlive(entry.Entity))
                return ResolveResult.Unreachable;

            target = entry.Entity;
            return ResolveResult.Resolved;
        }

        private void ApplyPlate(Entity entity, PBAvatarNametag pbNametag, Entity target)
        {
            // Remote-player entity ids are recycled, so a re-resolve can land on a different avatar
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
            World.AddOrSet(entity, new SceneAvatarTagApplied(target));
        }

        private void MarkPlateRemoving(Entity target)
        {
            if (!globalWorld.IsAlive(target)) return;

            ref SceneAvatarTagComponent plate = ref globalWorld.TryGetRef<SceneAvatarTagComponent>(target, out bool exists);

            if (exists)
                plate.IsRemoving = true;
        }

        private enum ResolveResult
        {
            Resolved,

            /// <summary>The avatar behind the scene entity does not exist yet, but still can.</summary>
            Pending,

            /// <summary>No avatar backs the scene entity, and none ever will.</summary>
            Unreachable,

            /// <summary>Only a scan over the player entities can tell which of the other outcomes applies.</summary>
            NeedsPlayerScan,
        }

        private struct PendingScan
        {
            public readonly Entity SceneEntity;
            public readonly CRDTEntity CrdtEntity;
            public Entity PlayerEntity;

            public PendingScan(Entity sceneEntity, CRDTEntity crdtEntity)
            {
                SceneEntity = sceneEntity;
                CrdtEntity = crdtEntity;
                PlayerEntity = Entity.Null;
            }
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
