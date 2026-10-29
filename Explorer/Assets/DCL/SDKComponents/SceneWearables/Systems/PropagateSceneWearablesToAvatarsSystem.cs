using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.SceneWorld;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using Google.Protobuf.Collections;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility.Arch;
using Color3 = Decentraland.Common.Color3;
using PendingScan = DCL.Multiplayer.SDK.Systems.SceneWorld.ScenePlayerAvatarResolver.PendingScan;
using ResolveResult = DCL.Multiplayer.SDK.Systems.SceneWorld.ScenePlayerAvatarResolver.Result;

namespace DCL.SDKComponents.SceneWearables.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(ResolveSceneWearablePathsSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    [ThrottlingEnabled]
    public partial class PropagateSceneWearablesToAvatarsSystem : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly World globalWorld;
        private readonly ScenePlayerAvatarResolver avatarResolver;
        private readonly string sceneWearableUrnPrefix;
        private readonly List<PendingScan> pendingScans = new ();

        internal PropagateSceneWearablesToAvatarsSystem(World world, ISceneData sceneData, ISceneStateProvider sceneStateProvider,
            IReadOnlyEntityParticipantTable entityParticipantTable, World globalWorld, Entity globalPlayerEntity) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.globalWorld = globalWorld;
            avatarResolver = new ScenePlayerAvatarResolver(globalWorld, globalPlayerEntity, entityParticipantTable);
            sceneWearableUrnPrefix = SceneWearableDTOFactory.GetUrnPrefix(sceneData.SceneEntityDefinition.id);
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            if (isCurrent)
                MarkPlayerAvatarShapesDirtyQuery(World);
            else
            {
                ClearAppliedOverridesQuery(World);
                StopAllTrackingQuery(World);
            }
        }

        public void FinalizeComponents(in Query query) =>
            ClearAppliedOverridesQuery(World);

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            HandleComponentRemovedQuery(World);
            HandleEntityDestructionQuery(World);
            HandlePlayerLeftQuery(World);
            StopTrackingOnComponentRemovedQuery(World);

            var newPlayersCount = 0;
            TagNewPlayersQuery(World, ref newPlayersCount);

            if (newPlayersCount > 0)
                RearmDetachedOverridesQuery(World);

            pendingScans.Clear();
            PropagateOverrideQuery(World);
            RetryPendingOverrideQuery(World);

            if (pendingScans.Count == 0) return;

            MatchPendingPlayersQuery(World);
            ResolvePendingScans();
        }

        [Query]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoved(Entity entity, in SceneWearablesApplied applied) =>
            DropOverride(entity, in applied);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(Entity entity, in SceneWearablesApplied applied) =>
            DropOverride(entity, in applied);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandlePlayerLeft(Entity entity, in SceneWearablesApplied applied)
        {
            if (applied.PlayerEntity == Entity.Null) return;

            if (World.IsAlive(applied.PlayerEntity) && !World.Has<DeleteEntityIntention>(applied.PlayerEntity)) return;

            DropOverride(entity, in applied);
            World.Add(entity, new SceneWearablesDetached());
        }

        [Query]
        private void ClearAppliedOverrides(Entity entity, in SceneWearablesApplied applied) =>
            DropOverride(entity, in applied);

        [Query]
        [Any(typeof(SceneWearablesPending), typeof(SceneWearablesDetached))]
        [None(typeof(PBAvatarShape))]
        private void StopTrackingOnComponentRemoved(Entity entity) =>
            StopTracking(entity);

        [Query]
        [Any(typeof(SceneWearablesPending), typeof(SceneWearablesDetached))]
        private void StopAllTracking(Entity entity) =>
            StopTracking(entity);

        [Query]
        [All(typeof(PlayerSceneCRDTEntity))]
        [None(typeof(PlayerSeen), typeof(DeleteEntityIntention))]
        private void TagNewPlayers([Data] ref int newPlayersCount, Entity entity)
        {
            newPlayersCount++;
            World.Add(entity, new PlayerSeen());
        }

        [Query]
        [All(typeof(SceneWearablesDetached))]
        [None(typeof(DeleteEntityIntention))]
        private void RearmDetachedOverrides(Entity entity, ref PBAvatarShape pbAvatarShape)
        {
            pbAvatarShape.IsDirty = true;
            World.Remove<SceneWearablesDetached>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateOverride(Entity entity, in CRDTEntity crdtEntity, in PBAvatarShape pbAvatarShape)
        {
            if (!pbAvatarShape.IsDirty || !IsPlayerEntity(in crdtEntity)) return;

            TryPropagate(entity, in crdtEntity, pbAvatarShape);
        }

        [Query]
        [All(typeof(SceneWearablesPending))]
        [None(typeof(DeleteEntityIntention))]
        private void RetryPendingOverride(Entity entity, in CRDTEntity crdtEntity, in PBAvatarShape pbAvatarShape)
        {
            if (pbAvatarShape.IsDirty) return;

            TryPropagate(entity, in crdtEntity, pbAvatarShape);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void MatchPendingPlayers(Entity entity, in PlayerSceneCRDTEntity playerCrdtEntity) =>
            ScenePlayerAvatarResolver.MatchPlayer(pendingScans, entity, in playerCrdtEntity);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void MarkPlayerAvatarShapesDirty(in CRDTEntity crdtEntity, in PBAvatarShape pbAvatarShape)
        {
            if (IsPlayerEntity(in crdtEntity))
                pbAvatarShape.IsDirty = true;
        }

        private void TryPropagate(Entity entity, in CRDTEntity crdtEntity, PBAvatarShape pbAvatarShape)
        {
            bool isLocalPlayer = crdtEntity.Id == SpecialEntitiesID.PLAYER_ENTITY;
            Entity target;

            ResolveResult resolveResult = isLocalPlayer
                ? avatarResolver.ResolveLocalPlayer(out target)
                : avatarResolver.ResolveRemotePlayer(World, entity, out target);

            switch (resolveResult)
            {
                case ResolveResult.Resolved:
                    ApplyOverride(entity, pbAvatarShape, target, isLocalPlayer ? Entity.Null : entity);
                    break;
                case ResolveResult.Pending:
                    World.AddOrGet(entity, new SceneWearablesPending());
                    break;
                case ResolveResult.Unreachable:
                    World.TryRemove<SceneWearablesPending>(entity);
                    break;
                case ResolveResult.NeedsPlayerScan:
                    pendingScans.Add(new PendingScan(entity, crdtEntity));
                    break;
            }
        }

        private void ResolvePendingScans()
        {
            for (var i = 0; i < pendingScans.Count; i++)
            {
                PendingScan scan = pendingScans[i];

                if (!World.IsAlive(scan.SceneEntity) || !World.TryGet(scan.SceneEntity, out PBAvatarShape? pbAvatarShape) || pbAvatarShape == null)
                    continue;

                switch (avatarResolver.ResolveScannedPlayer(World, in scan, out Entity target))
                {
                    case ResolveResult.Resolved:
                        ApplyOverride(scan.SceneEntity, pbAvatarShape, target, scan.PlayerEntity);
                        break;
                    case ResolveResult.Pending:
                        World.AddOrGet(scan.SceneEntity, new SceneWearablesPending());
                        break;
                    default:
                        World.TryRemove<SceneWearablesPending>(scan.SceneEntity);
                        break;
                }
            }
        }

        private void ApplyOverride(Entity entity, PBAvatarShape pbAvatarShape, Entity target, Entity playerEntity)
        {
            // Remote-player entity ids are recycled, so a re-resolve can land on a different avatar than the one that wears the override.
            if (World.TryGet(entity, out SceneWearablesApplied applied) && applied.GlobalEntity != target)
                ClearOverride(applied.GlobalEntity);

            CopyOverride(target, pbAvatarShape);

            World.AddOrSet(entity, new SceneWearablesApplied(target, playerEntity));
            World.TryRemove<SceneWearablesPending>(entity);
        }

        private void DropOverride(Entity entity, in SceneWearablesApplied applied)
        {
            Entity target = applied.GlobalEntity;

            World.Remove<SceneWearablesApplied>(entity);
            ClearOverride(target);
        }

        private void StopTracking(Entity entity)
        {
            World.TryRemove<SceneWearablesPending>(entity);
            World.TryRemove<SceneWearablesDetached>(entity);
        }

        private void CopyOverride(Entity target, PBAvatarShape pbAvatarShape)
        {
            if (!globalWorld.Has<SceneAvatarShapeOverride>(target))
            {
                if (!OverridesAnything(pbAvatarShape)) return;

                globalWorld.Add(target, new SceneAvatarShapeOverride(new List<URN>()));
            }

            ref SceneAvatarShapeOverride sceneOverride = ref globalWorld.Get<SceneAvatarShapeOverride>(target);

            if (TryCopyOverride(pbAvatarShape, ref sceneOverride))
                sceneOverride.IsDirty = true;
        }

        private void ClearOverride(Entity target)
        {
            if (!globalWorld.IsAlive(target)) return;

            ref SceneAvatarShapeOverride sceneOverride = ref globalWorld.TryGetRef<SceneAvatarShapeOverride>(target, out bool exists);

            if (!exists || sceneOverride.IsEmpty) return;

            sceneOverride.Clear();
            sceneOverride.IsDirty = true;
        }

        private bool TryCopyOverride(PBAvatarShape pbAvatarShape, ref SceneAvatarShapeOverride sceneOverride)
        {
            BodyShape? bodyShape = pbAvatarShape.HasBodyShape ? (BodyShape)pbAvatarShape : (BodyShape?)null;
            Color? skinColor = ToColor(pbAvatarShape.SkinColor);
            Color? hairColor = ToColor(pbAvatarShape.HairColor);
            Color? eyesColor = ToColor(pbAvatarShape.EyeColor);
            bool? showOnlyWearables = pbAvatarShape.HasShowOnlyWearables ? pbAvatarShape.ShowOnlyWearables : null;

            bool changed = TryCopyWearables(pbAvatarShape.Wearables, sceneOverride.Wearables);
            changed |= !Nullable.Equals(sceneOverride.BodyShape, bodyShape);
            changed |= sceneOverride.SkinColor != skinColor;
            changed |= sceneOverride.HairColor != hairColor;
            changed |= sceneOverride.EyesColor != eyesColor;
            changed |= sceneOverride.ShowOnlyWearables != showOnlyWearables;

            sceneOverride.BodyShape = bodyShape;
            sceneOverride.SkinColor = skinColor;
            sceneOverride.HairColor = hairColor;
            sceneOverride.EyesColor = eyesColor;
            sceneOverride.ShowOnlyWearables = showOnlyWearables;

            return changed;
        }

        private bool TryCopyWearables(RepeatedField<string> wearables, List<URN> target)
        {
            var allowedCount = 0;
            var changed = false;

            for (var i = 0; i < wearables.Count; i++)
            {
                string wearable = wearables[i];

                if (!IsAllowedWearable(wearable)) continue;

                if (allowedCount >= target.Count || !string.Equals(target[allowedCount].ToString(), wearable, StringComparison.OrdinalIgnoreCase))
                    changed = true;

                allowedCount++;
            }

            if (!changed && allowedCount == target.Count)
                return false;

            target.Clear();

            for (var i = 0; i < wearables.Count; i++)
                if (IsAllowedWearable(wearables[i]))
                    target.Add(new URN(wearables[i]));

            return true;
        }

        private bool OverridesAnything(PBAvatarShape pbAvatarShape)
        {
            if (pbAvatarShape.HasBodyShape || pbAvatarShape.HasShowOnlyWearables
                || pbAvatarShape.SkinColor != null || pbAvatarShape.HairColor != null || pbAvatarShape.EyeColor != null)
                return true;

            RepeatedField<string> wearables = pbAvatarShape.Wearables;

            for (var i = 0; i < wearables.Count; i++)
                if (IsAllowedWearable(wearables[i]))
                    return true;

            return false;
        }

        private bool IsAllowedWearable(string pointer) =>
            !SceneWearableDTOFactory.IsSceneWearableUrn(pointer) || pointer.StartsWith(sceneWearableUrnPrefix, StringComparison.OrdinalIgnoreCase);

        private static Color? ToColor(Color3? color) =>
            color == null ? null : new Color(color.R, color.G, color.B);

        private static bool IsPlayerEntity(in CRDTEntity crdtEntity) =>
            crdtEntity.Id == SpecialEntitiesID.PLAYER_ENTITY || SpecialEntitiesID.IsOtherPlayerEntity(crdtEntity.EntityNumber);

        private struct SceneWearablesApplied
        {
            public readonly Entity GlobalEntity;

            // The remote player's scene entity that holds its profile. Entity.Null for the local player.
            public readonly Entity PlayerEntity;

            public SceneWearablesApplied(Entity globalEntity, Entity playerEntity)
            {
                GlobalEntity = globalEntity;
                PlayerEntity = playerEntity;
            }
        }

        private struct SceneWearablesPending { }

        private struct SceneWearablesDetached { }

        private struct PlayerSeen { }
    }
}
