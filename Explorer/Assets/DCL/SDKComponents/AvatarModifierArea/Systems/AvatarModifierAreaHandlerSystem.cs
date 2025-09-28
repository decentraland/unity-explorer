using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.SDKEntityTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Profiles;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.AvatarModifierArea.Components;
using DCL.Web3.Identities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility.Arch;

namespace DCL.SDKComponents.AvatarModifierArea.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class AvatarModifierAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly World globalWorld;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private Transform? localAvatarTransform;
        private Entity ownAvatarEntity;

        public AvatarModifierAreaHandlerSystem(World world, World globalWorld,
            ISceneRestrictionBusController sceneRestrictionBusController,
            IWeb3IdentityCache web3IdentityCache) : base(world)
        {
            this.globalWorld = globalWorld;
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
        [All(typeof(AvatarModifierAreaComponent))]
        private void ResetAffectedEntities(in Entity entity, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            foreach (Collider avatarCollider in triggerAreaComponent.CurrentEntitiesInside)
            {
                Transform avatarTransform = avatarCollider.transform;
                if (!TryGetAvatarEntity(avatarTransform, out Entity avatarEntity)) continue;

                ShowAvatar(avatarEntity, avatarTransform);
                EnableAvatarInteraction(avatarEntity);
            }

            World!.Remove<AvatarModifierAreaComponent>(entity);
        }

        [Query]
        [None(typeof(SDKEntityTriggerAreaComponent), typeof(AvatarModifierAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupAvatarModifierArea(in Entity entity, ref PBAvatarModifierArea pbAvatarModifierArea)
        {
            World!.Add(entity,
                new SDKEntityTriggerAreaComponent(areaSize: pbAvatarModifierArea.Area, targetOnlyMainPlayer: false),
                new AvatarModifierAreaComponent(pbAvatarModifierArea.ExcludeIds!)
            );
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateAvatarModifierArea(ref PBAvatarModifierArea pbAvatarModifierArea,
            ref AvatarModifierAreaComponent modifierAreaComponent,
            ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            bool isHideAvatarsType = pbAvatarModifierArea.Modifiers.Contains(AvatarModifierType.AmtHideAvatars);
            bool isHidePassportsType = pbAvatarModifierArea.Modifiers.Contains(AvatarModifierType.AmtDisablePassports);

            if (pbAvatarModifierArea.IsDirty)
            {
                pbAvatarModifierArea.IsDirty = false;
                triggerAreaComponent.UpdateAreaSize(pbAvatarModifierArea.Area);
                modifierAreaComponent.SetExcludedIds(pbAvatarModifierArea.ExcludeIds!);

                // Update effect on now excluded/non-excluded avatars
                foreach (Collider avatarCollider in triggerAreaComponent.CurrentEntitiesInside)
                {
                    Transform avatarTransform = avatarCollider.transform;
                    if (!TryGetAvatarEntity(avatarTransform, out var entity)) continue;

                    if (isHideAvatarsType)
                        HideAvatar(entity, avatarTransform, modifierAreaComponent.ExcludedIds);

                    if (isHidePassportsType)
                        DisableAvatarInteraction(entity, modifierAreaComponent.ExcludedIds);
                }
            }

            foreach (Collider avatarCollider in triggerAreaComponent.ExitedEntitiesToBeProcessed)
            {
                Transform avatarTransform = avatarCollider.transform;
                if (!TryGetAvatarEntity(avatarTransform, out var entity)) continue;

                ShowAvatar(entity, avatarTransform);
                EnableAvatarInteraction(entity);
            }

            triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();

            foreach (Collider avatarCollider in triggerAreaComponent.EnteredEntitiesToBeProcessed)
            {
                Transform avatarTransform = avatarCollider.transform;
                if (!TryGetAvatarEntity(avatarTransform, out var entity)) continue;

                if (isHideAvatarsType)
                    HideAvatar(entity, avatarTransform, modifierAreaComponent.ExcludedIds);

                if (isHidePassportsType)
                    DisableAvatarInteraction(entity, modifierAreaComponent.ExcludedIds);
            }

            triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBAvatarModifierArea))]
        private void HandleEntityDestruction(ref SDKEntityTriggerAreaComponent triggerAreaComponent,
            ref AvatarModifierAreaComponent modifierComponent)
        {
            // Reset state of affected entities
            foreach (Collider avatarCollider in triggerAreaComponent.CurrentEntitiesInside)
            {
                Transform avatarTransform = avatarCollider.transform;
                if (!TryGetAvatarEntity(avatarTransform, out var entity)) continue;

                ShowAvatar(entity, avatarTransform);
                EnableAvatarInteraction(entity);
            }

            modifierComponent.Dispose();
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBAvatarModifierArea))]
        private void HandleComponentRemoval(in Entity entity, ref SDKEntityTriggerAreaComponent triggerAreaComponent,
            ref AvatarModifierAreaComponent modifierComponent)
        {
            // Reset state of affected entities
            foreach (Collider avatarCollider in triggerAreaComponent.CurrentEntitiesInside)
            {
                Transform avatarTransform = avatarCollider.transform;
                if (!TryGetAvatarEntity(avatarTransform, out var avatarEntity)) continue;

                ShowAvatar(avatarEntity, avatarTransform);
                EnableAvatarInteraction(avatarEntity);
            }

            modifierComponent.Dispose();

            World!.Remove<AvatarModifierAreaComponent>(entity);
        }

        private void ShowAvatar(Entity entity, Transform avatarTransform)
        {
            ref AvatarShapeComponent avatarShape = ref globalWorld.TryGetRef<AvatarShapeComponent>(entity, out bool hasAvatarShape);
            if (!hasAvatarShape) return;

            avatarShape.HiddenByModifierArea = false;

            if (avatarTransform == localAvatarTransform)
            {
                localAvatarTransform = null;
                sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateAvatarHidden(SceneRestrictionsAction.REMOVED));
            }
        }

        private void HideAvatar(Entity entity, Transform avatarTransform, HashSet<string> excludedIds)
        {
            if (!globalWorld.TryGet(entity, out Profile? profile)) return;

            ref AvatarShapeComponent avatarShape = ref globalWorld.TryGetRef<AvatarShapeComponent>(entity, out bool hasAvatarShape);
            if (!hasAvatarShape) return;

            bool shouldHide = !excludedIds.Contains(profile!.UserId);
            avatarShape.HiddenByModifierArea = shouldHide;

            if (shouldHide && profile.UserId == web3IdentityCache.Identity?.Address)
            {
                localAvatarTransform = avatarTransform;
                sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateAvatarHidden(SceneRestrictionsAction.APPLIED));
            }
        }

        private void DisableAvatarInteraction(Entity entity, HashSet<string> excludedIds)
        {
            if (!globalWorld.TryGet(entity, out Profile? profile)) return;

            bool shouldDisable = !excludedIds.Contains(profile!.UserId);

            if (shouldDisable)
            {
                // Something like TryAdd
                globalWorld.AddOrGet<IgnoreInteractionComponent>(entity);

                if (profile.UserId == web3IdentityCache.Identity?.Address)
                {
                    ownAvatarEntity = entity;
                    sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreatePassportCannotBeOpened(SceneRestrictionsAction.APPLIED));
                }
            }
            else
                EnableAvatarInteraction(entity);
        }

        private void EnableAvatarInteraction(Entity entity)
        {
            globalWorld.TryRemove<IgnoreInteractionComponent>(entity);

            if (ownAvatarEntity == entity)
                sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreatePassportCannotBeOpened(SceneRestrictionsAction.REMOVED));
        }

        private bool TryGetAvatarEntity(Transform transform, out Entity entity)
        {
            entity = Entity.Null;
            var result = FindAvatarUtils.AvatarWithTransform(globalWorld, transform);
            if (!result.Success) return false;
            entity = result.Result;
            return true;
        }
    }
}
