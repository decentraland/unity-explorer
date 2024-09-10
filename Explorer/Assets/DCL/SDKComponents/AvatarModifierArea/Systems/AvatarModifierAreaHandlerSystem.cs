using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterTriggerArea.Components;
using DCL.CharacterTriggerArea.Systems;
using DCL.Diagnostics;
using DCL.ECSComponents;
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
    [UpdateBefore(typeof(CharacterTriggerAreaCleanUpRegisteredCollisionsSystem))]
    [LogCategory(ReportCategory.CAMERA_MODE_AREA)]
    public partial class AvatarModifierAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static readonly QueryDescription AVATAR_BASE_QUERY = new QueryDescription().WithAll<AvatarBase>();
        private readonly AvatarFindQuery<(Transform avatarTransform, bool shouldHide, HashSet<string> excludedIds)> toggleAvatarHidingQuery;
        private readonly AvatarFindQuery<(Transform avatarTransform, HashSet<string> excludedIds)> correctAvatarHidingStateQuery;

        public AvatarModifierAreaHandlerSystem(World world, World globalWorld) : base(world)
        {
            toggleAvatarHidingQuery = new AvatarFindQuery<(Transform avatarTransform, bool shouldHide, HashSet<string> excludedIds)>(
                globalWorld,
                static (globalWorld, entity, context) =>
                {
                    Transform entityTransform = globalWorld.Get<AvatarBase>(entity).transform.parent;
                    if (context.avatarTransform != entityTransform) return false;

                    if (globalWorld.TryGet(entity, out Profile? profile) && context.excludedIds!.Contains(profile!.UserId))
                        return true;

                    globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea = context.shouldHide;
                    return true;
                }
            );

            correctAvatarHidingStateQuery = new AvatarFindQuery<(Transform avatarTransform, HashSet<string> excludedIds)>(
                globalWorld,
                static (globalWorld, entity, context) =>
                {
                    Transform entityTransform = globalWorld.Get<AvatarBase>(entity).transform.parent;
                    if (context.avatarTransform != entityTransform) return false;

                    if (!globalWorld.TryGet(entity, out Profile? profile))
                        return true;

                    globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea = context.excludedIds!.Contains(profile!.UserId) == false;
                    return true;
                }
            );
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

            foreach (Transform avatarTransform in triggerAreaComponent.ExitedThisFrame) { ToggleAvatarHiding(avatarTransform, false, modifierAreaComponent.ExcludedIds); }

            foreach (Transform avatarTransform in triggerAreaComponent.EnteredThisFrame) { ToggleAvatarHiding(avatarTransform, true, modifierAreaComponent.ExcludedIds); }
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
            toggleAvatarHidingQuery.Execute((avatarTransform, shouldHide, excludedIds));
        }

        internal void CorrectAvatarHidingState(Transform avatarTransform, HashSet<string> excludedIds)
        {
            correctAvatarHidingStateQuery.Execute((avatarTransform, excludedIds));
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

        private class AvatarFindQuery<TContext>
        {
            public delegate bool FindAction(World globalWorld, Entity entity, TContext context);

            private readonly World globalWorld;
            private readonly FindAction findAction;

            public AvatarFindQuery(World globalWorld, FindAction findAction)
            {
                this.globalWorld = globalWorld;
                this.findAction = findAction;
            }

            private TContext currentContext = default!;
            private bool found;

            public void Execute(TContext context)
            {
                currentContext = context;
                found = false;

                // There's no way to do a Query/InlineQuery getting both entity and TransformComponent...
                globalWorld.Query(in AVATAR_BASE_QUERY, Foreach);
            }

            private void Foreach(Entity entity)
            {
                if (found) return;
                found = findAction(globalWorld, entity, currentContext);
            }
        }
    }
}
