using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Loading.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
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

namespace DCL.SDKComponents.SceneWearables.Systems
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateBefore(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class PropagateSceneWearablesToAvatarsSystem : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
    {
        private const int MAX_PLAYER_WEARABLES = 32;

        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;
        private readonly World globalWorld;

        internal PropagateSceneWearablesToAvatarsSystem(World world, ISceneStateProvider sceneStateProvider,
            IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, World globalWorld) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.entitiesMap = entitiesMap;
            this.globalWorld = globalWorld;
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            if (isCurrent)
                MarkPlayerAvatarShapesDirtyQuery(World);
            else
                ClearAppliedOverridesQuery(World);
        }

        public void FinalizeComponents(in Query query) =>
            ClearAppliedOverridesQuery(World);

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            HandleComponentRemovedQuery(World);
            HandleEntityDestructionQuery(World);
            HandlePlayerLeftQuery(World);

            var anyPlayerShapeDirty = false;
            FindDirtyPlayerAvatarShapeQuery(World, ref anyPlayerShapeDirty);
            PropagateOverrideQuery(World, anyPlayerShapeDirty);
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
        [All(typeof(DeleteEntityIntention))]
        private void HandlePlayerLeft(Entity bridgeEntity, in PlayerSceneCRDTEntity player)
        {
            if (!entitiesMap.TryGetValue(player.CRDTEntity, out Entity entity)
                || !World.TryGet(entity, out SceneWearablesApplied applied)
                || applied.BridgeEntity != bridgeEntity)
                return;

            DropOverride(entity, in applied);
        }

        [Query]
        private void ClearAppliedOverrides(Entity entity, in SceneWearablesApplied applied) =>
            DropOverride(entity, in applied);

        // For a remote player the scene's PBAvatarShape lives on a different entity than PlayerSceneCRDTEntity: the two share only the CRDT id.
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateOverride([Data] bool anyPlayerShapeDirty, Entity bridgeEntity, in PlayerSceneCRDTEntity player)
        {
            if (!player.IsDirty && !anyPlayerShapeDirty) return;

            if (!entitiesMap.TryGetValue(player.CRDTEntity, out Entity entity)
                || !World.TryGet(entity, out PBAvatarShape? pbAvatarShape)
                || pbAvatarShape == null)
                return;

            if ((!player.IsDirty && !pbAvatarShape.IsDirty) || !globalWorld.IsAlive(player.GlobalEntity)) return;

            ApplyOverride(entity, pbAvatarShape, player.GlobalEntity, bridgeEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void FindDirtyPlayerAvatarShape([Data] ref bool found, in CRDTEntity crdtEntity, in PBAvatarShape pbAvatarShape)
        {
            if (pbAvatarShape.IsDirty && SpecialEntitiesID.IsAnyPlayerEntity(crdtEntity))
                found = true;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void MarkPlayerAvatarShapesDirty(in CRDTEntity crdtEntity, in PBAvatarShape pbAvatarShape)
        {
            if (SpecialEntitiesID.IsAnyPlayerEntity(crdtEntity))
                pbAvatarShape.IsDirty = true;
        }

        private void ApplyOverride(Entity entity, PBAvatarShape pbAvatarShape, Entity target, Entity bridgeEntity)
        {
            // Remote-player entity ids are recycled, so the id can land on a different avatar than the one that wears the override.
            if (World.TryGet(entity, out SceneWearablesApplied applied) && applied.GlobalEntity != target)
                ClearOverride(applied.GlobalEntity);

            CopyOverride(target, pbAvatarShape);
            pbAvatarShape.IsDirty = false;

            World.AddOrSet(entity, new SceneWearablesApplied(target, bridgeEntity));
        }

        private void DropOverride(Entity entity, in SceneWearablesApplied applied)
        {
            Entity target = applied.GlobalEntity;

            World.Remove<SceneWearablesApplied>(entity);
            ClearOverride(target);
        }

        private void CopyOverride(Entity target, PBAvatarShape pbAvatarShape)
        {
            if (!globalWorld.Has<SceneAvatarShapeOverride>(target))
            {
                if (!OverridesAnything(pbAvatarShape)) return;

                globalWorld.Add(target, new SceneAvatarShapeOverride(new List<URN>()));
            }

            ref SceneAvatarShapeOverride sceneOverride = ref globalWorld.Get<SceneAvatarShapeOverride>(target);

            if (SyncOverride(pbAvatarShape, ref sceneOverride))
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

        private bool SyncOverride(PBAvatarShape pbAvatarShape, ref SceneAvatarShapeOverride sceneOverride)
        {
            BodyShape? bodyShape = pbAvatarShape.HasBodyShape ? (BodyShape)pbAvatarShape : (BodyShape?)null;
            Color? skinColor = ToColor(pbAvatarShape.SkinColor);
            Color? hairColor = ToColor(pbAvatarShape.HairColor);
            Color? eyesColor = ToColor(pbAvatarShape.EyeColor);
            bool? showOnlyWearables = pbAvatarShape.HasShowOnlyWearables ? pbAvatarShape.ShowOnlyWearables : null;

            bool changed = SyncWearables(pbAvatarShape.Wearables, sceneOverride.Wearables);
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

        private bool SyncWearables(RepeatedField<string> wearables, List<URN> target)
        {
            int count = Math.Min(wearables.Count, MAX_PLAYER_WEARABLES);
            bool changed = count != target.Count;

            for (var i = 0; !changed && i < count; i++)
                changed = !string.Equals(target[i].ToString(), wearables[i], StringComparison.OrdinalIgnoreCase);

            if (!changed) return false;

            if (wearables.Count > MAX_PLAYER_WEARABLES)
                ReportHub.LogWarning(GetReportData(), $"AvatarShape on a player has {wearables.Count} wearables, only the first {MAX_PLAYER_WEARABLES} apply");

            target.Clear();

            for (var i = 0; i < count; i++)
                target.Add(new URN(wearables[i]));

            return true;
        }

        private static bool OverridesAnything(PBAvatarShape pbAvatarShape) =>
            pbAvatarShape.HasBodyShape || pbAvatarShape.HasShowOnlyWearables || pbAvatarShape.Wearables.Count > 0
            || pbAvatarShape.SkinColor != null || pbAvatarShape.HairColor != null || pbAvatarShape.EyeColor != null;

        private static Color? ToColor(Color3? color) =>
            color == null ? null : new Color(color.R, color.G, color.B);

        private struct SceneWearablesApplied
        {
            public readonly Entity GlobalEntity;

            // A left player's bridge entity outlives the leave until its deletion runs, and may overlap with the bridge entity
            // of the player's re-entry: only the leave of the bridge entity that applied the override may drop it.
            public readonly Entity BridgeEntity;

            public SceneWearablesApplied(Entity globalEntity, Entity bridgeEntity)
            {
                GlobalEntity = globalEntity;
                BridgeEntity = bridgeEntity;
            }
        }
    }
}
