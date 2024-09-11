using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterTriggerArea.Components;
using DCL.CharacterTriggerArea.Systems;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.Typing;
using DCL.Profiles;
using DCL.SDKComponents.AvatarModifierArea.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.AvatarModifierArea.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class AvatarModifierAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static readonly QueryDescription AVATAR_BASE_QUERY = new QueryDescription().WithAll<AvatarBase>();
        private readonly World globalWorld;
        private readonly FindAvatarQuery findAvatarQuery;

        public AvatarModifierAreaHandlerSystem(World world, World globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
            findAvatarQuery = new FindAvatarQuery(globalWorld);
        }

        protected override void Update(float t)
        {
            UpdateAvatarModifierAreaQuery(World!);
            SetupAvatarModifierAreaQuery(World!);

            HandleEntityDestructionQuery(World!);
            HandleComponentRemovalQuery(World!);
        }

        [Query]
        [None(typeof(CharacterTriggerAreaComponent), typeof(AvatarModifierAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupAvatarModifierArea(in Entity entity, ref PBAvatarModifierArea pbAvatarModifierArea)
        {
            World!.Add(entity,
                new CharacterTriggerAreaComponent(areaSize: pbAvatarModifierArea.Area, targetOnlyMainPlayer: false),
                new AvatarModifierAreaComponent(pbAvatarModifierArea.ExcludeIds!)
            );
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateAvatarModifierArea(ref PBAvatarModifierArea pbAvatarModifierArea, ref AvatarModifierAreaComponent modifierAreaComponent, ref CharacterTriggerAreaComponent triggerAreaComponent)
        {
            if (pbAvatarModifierArea.IsDirty)
            {
                pbAvatarModifierArea.IsDirty = false;
                triggerAreaComponent.UpdateAreaSize(pbAvatarModifierArea.Area);
                modifierAreaComponent.SetExcludedIds(pbAvatarModifierArea.ExcludeIds!);

                // Update effect on now excluded/non-excluded avatars
                foreach (Transform avatarTransform in triggerAreaComponent.CurrentAvatarsInside) { CorrectAvatarHidingState(avatarTransform, modifierAreaComponent.ExcludedIds); }
            }

            foreach (Transform avatarTransform in triggerAreaComponent.ExitedAvatarsToBeProcessed) { ToggleAvatarHiding(avatarTransform, false, modifierAreaComponent.ExcludedIds); }
            triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();

            foreach (Transform avatarTransform in triggerAreaComponent.EnteredAvatarsToBeProcessed) { ToggleAvatarHiding(avatarTransform, true, modifierAreaComponent.ExcludedIds); }
            triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBAvatarModifierArea))]
        private void HandleEntityDestruction(ref CharacterTriggerAreaComponent triggerAreaComponent, ref AvatarModifierAreaComponent modifierComponent)
        {
            // Reset state of affected entities
            foreach (Transform avatarTransform in triggerAreaComponent.CurrentAvatarsInside) { ToggleAvatarHiding(avatarTransform, false, modifierComponent.ExcludedIds); }

            modifierComponent.Dispose();
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBAvatarModifierArea))]
        private void HandleComponentRemoval(Entity e, ref CharacterTriggerAreaComponent triggerAreaComponent, ref AvatarModifierAreaComponent modifierComponent)
        {
            // Reset state of affected entities
            foreach (Transform avatarTransform in triggerAreaComponent.CurrentAvatarsInside)
                ToggleAvatarHiding(avatarTransform, false, modifierComponent.ExcludedIds);

            modifierComponent.Dispose();

            World!.Remove<AvatarModifierAreaComponent>(e);
        }

        internal void ToggleAvatarHiding(Transform avatarTransform, bool shouldHide, HashSet<string> excludedIds)
        {
            var result = findAvatarQuery.AvatarWithTransform(avatarTransform);
            if (!result.Success) return;

            var entity = result.Result;

            if (globalWorld.TryGet(entity, out Profile? profile) && excludedIds.Contains(profile!.UserId)) return;
            globalWorld.Get<AvatarShapeComponent>(entity).UpdateHiddenStatus(shouldHide);
        }

        internal void CorrectAvatarHidingState(Transform avatarTransform, HashSet<string> excludedIds)
        {
            var result = findAvatarQuery.AvatarWithTransform(avatarTransform);
            if (!result.Success) return;

            var entity = result.Result;

            if (globalWorld.TryGet(entity, out Profile? profile))
                globalWorld.Get<AvatarShapeComponent>(entity).UpdateHiddenStatus(excludedIds.Contains(profile!.UserId) == false);
        }

        [Query]
        private void FinalizeComponents(in Entity entity, ref CharacterTriggerAreaComponent triggerAreaComponent, ref AvatarModifierAreaComponent modifierComponent)
        {
            // Reset state of affected entities
            foreach (Transform avatarTransform in triggerAreaComponent.CurrentAvatarsInside)
                ToggleAvatarHiding(avatarTransform, false, modifierComponent.ExcludedIds);

            World!.Remove<AvatarModifierAreaComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World!);
        }

        private class FindAvatarQuery
        {
            private readonly World globalWorld;
            private readonly ForEach cachedFindEntity;

            private Entity foundedEntityOrNull = Entity.Null;
            private Transform? requiredTransform;

            public FindAvatarQuery(World globalWorld)
            {
                this.globalWorld = globalWorld;
                cachedFindEntity = this.FindEntity;
            }

            public LightResult<Entity> AvatarWithTransform(Transform avatarTransform)
            {
                foundedEntityOrNull = Entity.Null;
                requiredTransform = avatarTransform;
                globalWorld.Query(in AVATAR_BASE_QUERY, cachedFindEntity);

                return foundedEntityOrNull == Entity.Null
                    ? LightResult<Entity>.FAILURE
                    : new LightResult<Entity>(foundedEntityOrNull);
            }

            private void FindEntity(Entity entity)
            {
                if (foundedEntityOrNull != Entity.Null) return;
                if (globalWorld.Get<AvatarBase>(entity).transform.parent == requiredTransform) foundedEntityOrNull = entity;
            }
        }
    }
}
