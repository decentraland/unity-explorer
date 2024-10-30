using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.CharacterTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.Typing;
using DCL.Profiles;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SDKComponents.AvatarModifierArea.Components;
using DCL.Web3.Identities;
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
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private Transform? localAvatarTransform;

        public AvatarModifierAreaHandlerSystem(World world, World globalWorld, ISceneRestrictionBusController sceneRestrictionBusController, IWeb3IdentityCache web3IdentityCache) : base(world)
        {
            this.globalWorld = globalWorld;
            findAvatarQuery = new FindAvatarQuery(globalWorld);
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.web3IdentityCache = web3IdentityCache;
        }

        protected override void Update(float t)
        {
            UpdateAvatarModifierAreaQuery(World!);
            SetupAvatarModifierAreaQuery(World!);

            HandleEntityDestructionQuery(World!);
            HandleComponentRemovalQuery(World!);
        }

        public void FinalizeComponents(in Query query)
        {
            ResetAffectedEntitiesQuery(World!);
        }

        [Query]
        private void ResetAffectedEntities(in Entity entity, ref CharacterTriggerAreaComponent triggerAreaComponent, ref AvatarModifierAreaComponent modifierComponent)
        {
            foreach (Transform avatarTransform in triggerAreaComponent.CurrentAvatarsInside)
                ShowAvatar(avatarTransform);

            World!.Remove<AvatarModifierAreaComponent>(entity);
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
        private void UpdateAvatarModifierArea(Entity entity, ref PBAvatarModifierArea pbAvatarModifierArea, ref AvatarModifierAreaComponent modifierAreaComponent, ref CharacterTriggerAreaComponent triggerAreaComponent)
        {
            if (pbAvatarModifierArea.IsDirty)
            {
                pbAvatarModifierArea.IsDirty = false;
                triggerAreaComponent.UpdateAreaSize(pbAvatarModifierArea.Area);
                modifierAreaComponent.SetExcludedIds(pbAvatarModifierArea.ExcludeIds!);

                // Update effect on now excluded/non-excluded avatars
                foreach (Transform avatarTransform in triggerAreaComponent.CurrentAvatarsInside)
                    HideAvatar(avatarTransform, modifierAreaComponent.ExcludedIds);
            }

            foreach (Transform avatarTransform in triggerAreaComponent.ExitedAvatarsToBeProcessed)
                ShowAvatar(avatarTransform);

            triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();

            foreach (Transform avatarTransform in triggerAreaComponent.EnteredAvatarsToBeProcessed)
                HideAvatar(avatarTransform, modifierAreaComponent.ExcludedIds);

            triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBAvatarModifierArea))]
        private void HandleEntityDestruction(ref CharacterTriggerAreaComponent triggerAreaComponent, ref AvatarModifierAreaComponent modifierComponent)
        {
            // Reset state of affected entities
            foreach (Transform avatarTransform in triggerAreaComponent.CurrentAvatarsInside)
                ShowAvatar(avatarTransform);

            modifierComponent.Dispose();
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBAvatarModifierArea))]
        private void HandleComponentRemoval(Entity entity, ref CharacterTriggerAreaComponent triggerAreaComponent, ref AvatarModifierAreaComponent modifierComponent)
        {
            // Reset state of affected entities
            foreach (Transform avatarTransform in triggerAreaComponent.CurrentAvatarsInside)
                ShowAvatar(avatarTransform);

            modifierComponent.Dispose();

            World!.Remove<AvatarModifierAreaComponent>(entity);
        }

        private void ShowAvatar(Transform avatarTransform)
        {
            var result = findAvatarQuery.AvatarWithTransform(avatarTransform);
            if (!result.Success) return;

            var entity = result.Result;

            ref AvatarShapeComponent avatarShape = ref globalWorld.TryGetRef<AvatarShapeComponent>(entity, out bool hasAvatarShape);
            if (!hasAvatarShape) return;

            avatarShape.HiddenByModifierArea = false;
            if (avatarTransform == localAvatarTransform)
            {
                localAvatarTransform = null;
                sceneRestrictionBusController.PushSceneRestriction(new AvatarHiddenRestriction
                {
                    Action = SceneRestrictionsAction.REMOVED,
                });
            }
        }

        private void HideAvatar(Transform avatarTransform, HashSet<string> excludedIds)
        {
            var result = findAvatarQuery.AvatarWithTransform(avatarTransform);
            if (!result.Success) return;

            var entity = result.Result;

            if (!globalWorld.TryGet(entity, out Profile? profile)) return;

            ref AvatarShapeComponent avatarShape = ref globalWorld.TryGetRef<AvatarShapeComponent>(entity, out bool hasAvatarShape);
            if (!hasAvatarShape) return;

            bool shouldHide = !excludedIds.Contains(profile!.UserId);
            avatarShape.HiddenByModifierArea = shouldHide;

            if (shouldHide && profile.UserId == web3IdentityCache.Identity?.Address)
            {
                localAvatarTransform = avatarTransform;
                sceneRestrictionBusController.PushSceneRestriction(new AvatarHiddenRestriction
                {
                    Action = SceneRestrictionsAction.APPLIED,
                });
            }
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
                if (globalWorld.Get<AvatarBase>(entity).transform.parent != requiredTransform) return;
                foundedEntityOrNull = entity;
            }
        }
    }
}
