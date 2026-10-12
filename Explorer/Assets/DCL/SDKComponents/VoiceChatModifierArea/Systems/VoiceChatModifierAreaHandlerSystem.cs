using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Profiles;
using DCL.SDKComponents.VoiceChatModifierArea.Components;
using DCL.SDKEntityTriggerArea.Components;
using DCL.VoiceChat.Nearby;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.VoiceChatModifierArea.Systems
{
    /// <summary>
    ///     Writes the voice modifiers of each <see cref="PBVoiceChatModifierArea" /> onto the global-world avatars standing inside it.
    /// </summary>
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class VoiceChatModifierAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private const float MIN_MAX_DISTANCE = 0.1f;

        private static readonly QueryDescription REMOVED_AREA_QUERY = new QueryDescription()
                                                                     .WithAll<VoiceChatModifierAreaComponent>()
                                                                     .WithNone<DeleteEntityIntention, PBVoiceChatModifierArea>();

        private readonly World globalWorld;
        private readonly ISceneData sceneData;

        internal VoiceChatModifierAreaHandlerSystem(World world, World globalWorld, ISceneData sceneData) : base(world)
        {
            this.globalWorld = globalWorld;
            this.sceneData = sceneData;
        }

        protected override void Update(float t)
        {
            UpdateAreaQuery(World);
            SetupAreaQuery(World);

            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);
            World.Remove<VoiceChatModifierAreaComponent>(in REMOVED_AREA_QUERY);
        }

        public void FinalizeComponents(in Query query)
        {
            ReleaseAllAreasQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(SDKEntityTriggerAreaComponent), typeof(VoiceChatModifierAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupArea(Entity entity, in PBVoiceChatModifierArea pbArea)
        {
            World.Add(entity,
                new SDKEntityTriggerAreaComponent(areaSize: pbArea.Area, targetOnlyMainPlayer: false),
                new VoiceChatModifierAreaComponent(new HashSet<Collider>()));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(TransformComponent))]
        private void UpdateArea(Entity entity, ref PBVoiceChatModifierArea pbArea, ref SDKEntityTriggerAreaComponent triggerArea, in VoiceChatModifierAreaComponent area)
        {
            VoiceChatAreaModifier modifier = ToModifier(entity, pbArea);

            if (pbArea.IsDirty)
            {
                pbArea.IsDirty = false;
                triggerArea.UpdateAreaSize(pbArea.Area);

                foreach (Collider avatarCollider in area.AffectedAvatars)
                    ApplyToAvatar(avatarCollider, pbArea, modifier);
            }

            foreach (Collider avatarCollider in triggerArea.ExitedEntitiesToBeProcessed)
                if (area.AffectedAvatars.Remove(avatarCollider))
                    RemoveFromAvatar(avatarCollider, modifier);

            triggerArea.TryClearExitedAvatarsToBeProcessed();
            triggerArea.TryClearEnteredAvatarsToBeProcessed();

            if (triggerArea.CurrentEntitiesInside.Count == 0) return;

            foreach (Collider avatarCollider in triggerArea.CurrentEntitiesInside)
            {
                if (avatarCollider == null) continue;

                bool insideScene = IsInsideScene(avatarCollider.transform.position);

                if (insideScene == area.AffectedAvatars.Contains(avatarCollider)) continue;

                if (insideScene)
                {
                    area.AffectedAvatars.Add(avatarCollider);
                    ApplyToAvatar(avatarCollider, pbArea, modifier);
                }
                else
                {
                    area.AffectedAvatars.Remove(avatarCollider);
                    RemoveFromAvatar(avatarCollider, modifier);
                }
            }
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(Entity entity, in VoiceChatModifierAreaComponent area)
        {
            RemoveFromAllAffected(entity, area);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBVoiceChatModifierArea))]
        private void HandleComponentRemoval(Entity entity, in VoiceChatModifierAreaComponent area)
        {
            RemoveFromAllAffected(entity, area);
        }

        [Query]
        private void ReleaseAllAreas(Entity entity, in VoiceChatModifierAreaComponent area)
        {
            RemoveFromAllAffected(entity, area);
        }

        private void RemoveFromAllAffected(Entity entity, in VoiceChatModifierAreaComponent area)
        {
            var key = new VoiceChatAreaModifier(World, entity, 1f, float.PositiveInfinity, false, false);

            foreach (Collider avatarCollider in area.AffectedAvatars)
                RemoveFromAvatar(avatarCollider, key);

            area.AffectedAvatars.Clear();
        }

        private bool IsInsideScene(Vector3 position)
        {
            Vector2Int parcel = position.ToParcel();
            IReadOnlyList<Vector2Int> parcels = sceneData.Parcels;

            for (var i = 0; i < parcels.Count; i++)
                if (parcels[i] == parcel)
                    return true;

            return false;
        }

        private VoiceChatAreaModifier ToModifier(Entity entity, PBVoiceChatModifierArea pbArea) =>
            new (World, entity,
                pbArea.HasVolumeScale && float.IsFinite(pbArea.VolumeScale) ? Mathf.Clamp01(pbArea.VolumeScale) : 1f,
                pbArea.HasMaxDistance && float.IsFinite(pbArea.MaxDistance) ? Mathf.Max(pbArea.MaxDistance, MIN_MAX_DISTANCE) : float.PositiveInfinity,
                pbArea.HasMute && pbArea.Mute,
                pbArea.HasIsolate && pbArea.Isolate);

        private void ApplyToAvatar(Collider avatarCollider, PBVoiceChatModifierArea pbArea, in VoiceChatAreaModifier modifier)
        {
            if (!TryGetAvatarEntity(avatarCollider.transform, out Entity avatarEntity)) return;
            if (!globalWorld.TryGet(avatarEntity, out Profile? profile) || profile == null) return;

            if (IsExcluded(pbArea, profile.UserId.Value))
            {
                RemoveFromAvatar(avatarEntity, modifier);
                return;
            }

            if (!globalWorld.Has<VoiceChatAreaModifiersComponent>(avatarEntity))
                globalWorld.Add(avatarEntity, new VoiceChatAreaModifiersComponent(new List<VoiceChatAreaModifier>(2)));

            List<VoiceChatAreaModifier> modifiers = globalWorld.Get<VoiceChatAreaModifiersComponent>(avatarEntity).Modifiers;
            int index = IndexOfArea(modifiers, modifier);

            if (index >= 0)
                modifiers[index] = modifier;
            else
                modifiers.Add(modifier);
        }

        private void RemoveFromAvatar(Collider avatarCollider, in VoiceChatAreaModifier area)
        {
            if (avatarCollider != null && TryGetAvatarEntity(avatarCollider.transform, out Entity avatarEntity))
                RemoveFromAvatar(avatarEntity, area);
        }

        private void RemoveFromAvatar(Entity avatarEntity, in VoiceChatAreaModifier area)
        {
            if (!globalWorld.TryGet(avatarEntity, out VoiceChatAreaModifiersComponent component)) return;

            int index = IndexOfArea(component.Modifiers, area);

            if (index >= 0)
                component.Modifiers.RemoveAt(index);
        }

        private static int IndexOfArea(List<VoiceChatAreaModifier> modifiers, in VoiceChatAreaModifier area)
        {
            for (var i = 0; i < modifiers.Count; i++)
                if (modifiers[i].IsSameArea(area))
                    return i;

            return -1;
        }

        private static bool IsExcluded(PBVoiceChatModifierArea pbArea, string userId)
        {
            foreach (string excludedId in pbArea.ExcludeIds)
                if (string.Equals(excludedId, userId, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private bool TryGetAvatarEntity(Transform transform, out Entity entity)
        {
            var result = FindAvatarUtils.AvatarWithTransform(globalWorld, transform);
            entity = result.Success ? result.Result : Entity.Null;
            return result.Success;
        }
    }
}
