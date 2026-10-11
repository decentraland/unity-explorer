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
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.VoiceChatModifierArea.Systems
{
    /// <summary>
    ///     Writes the voice modifiers of each <see cref="PBVoiceChatModifierArea" /> onto the global-world avatars standing inside it.
    /// </summary>
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class VoiceChatModifierAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static readonly QueryDescription REMOVED_AREA_QUERY = new QueryDescription()
                                                                     .WithAll<VoiceChatModifierAreaComponent>()
                                                                     .WithNone<DeleteEntityIntention, PBVoiceChatModifierArea>();

        private readonly World globalWorld;

        internal VoiceChatModifierAreaHandlerSystem(World world, World globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
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
                new VoiceChatModifierAreaComponent());
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(TransformComponent), typeof(VoiceChatModifierAreaComponent))]
        private void UpdateArea(Entity entity, ref PBVoiceChatModifierArea pbArea, ref SDKEntityTriggerAreaComponent triggerArea)
        {
            VoiceChatAreaModifier modifier = ToModifier(entity, pbArea);

            if (pbArea.IsDirty)
            {
                pbArea.IsDirty = false;
                triggerArea.UpdateAreaSize(pbArea.Area);

                foreach (Collider avatarCollider in triggerArea.CurrentEntitiesInside)
                    ApplyToAvatar(avatarCollider, pbArea, modifier);
            }

            foreach (Collider avatarCollider in triggerArea.ExitedEntitiesToBeProcessed)
                RemoveFromAvatar(avatarCollider, modifier);

            triggerArea.TryClearExitedAvatarsToBeProcessed();

            foreach (Collider avatarCollider in triggerArea.EnteredEntitiesToBeProcessed)
                ApplyToAvatar(avatarCollider, pbArea, modifier);

            triggerArea.TryClearEnteredAvatarsToBeProcessed();
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(VoiceChatModifierAreaComponent))]
        private void HandleEntityDestruction(Entity entity, ref SDKEntityTriggerAreaComponent triggerArea)
        {
            RemoveFromAllInside(entity, triggerArea);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBVoiceChatModifierArea))]
        [All(typeof(VoiceChatModifierAreaComponent))]
        private void HandleComponentRemoval(Entity entity, ref SDKEntityTriggerAreaComponent triggerArea)
        {
            RemoveFromAllInside(entity, triggerArea);
        }

        [Query]
        [All(typeof(VoiceChatModifierAreaComponent))]
        private void ReleaseAllAreas(Entity entity, ref SDKEntityTriggerAreaComponent triggerArea)
        {
            RemoveFromAllInside(entity, triggerArea);
        }

        private void RemoveFromAllInside(Entity entity, in SDKEntityTriggerAreaComponent triggerArea)
        {
            var key = new VoiceChatAreaModifier(World, entity, 1f, float.PositiveInfinity, false, false);

            foreach (Collider avatarCollider in triggerArea.CurrentEntitiesInside)
                RemoveFromAvatar(avatarCollider, key);
        }

        private VoiceChatAreaModifier ToModifier(Entity entity, PBVoiceChatModifierArea pbArea) =>
            new (World, entity,
                pbArea.HasVolumeScale ? Mathf.Clamp01(pbArea.VolumeScale) : 1f,
                pbArea.HasMaxDistance ? pbArea.MaxDistance : float.PositiveInfinity,
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
            if (TryGetAvatarEntity(avatarCollider.transform, out Entity avatarEntity))
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
