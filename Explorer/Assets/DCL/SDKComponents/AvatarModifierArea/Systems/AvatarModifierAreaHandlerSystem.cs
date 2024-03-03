using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterTriggerArea.Components;
using DCL.CharacterTriggerArea.Systems;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Profiles;
using DCL.SDKComponents.AvatarModifierArea.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.AvatarModifierArea.Systems
{
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    [UpdateBefore(typeof(CharacterTriggerAreaCleanupSystem))]
    [LogCategory(ReportCategory.CAMERA_MODE_AREA)]
    public partial class AvatarModifierAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static readonly QueryDescription AVATAR_BASE_QUERY = new QueryDescription().WithAll<AvatarBase>();
        private readonly World globalWorld;

        public AvatarModifierAreaHandlerSystem(World world, ObjectProxy<World> globalWorldProxy) : base(world)
        {
            globalWorld = globalWorldProxy.Object;
        }

        protected override void Update(float t)
        {
            UpdateAvatarModifierAreaQuery(World);
            SetupAvatarModifierAreaQuery(World);

            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);

            World.Remove<AvatarModifierAreaComponent>(HandleComponentRemoval_QueryDescription);
            World.Remove<AvatarModifierAreaComponent, PBAvatarModifierArea>(HandleEntityDestruction_QueryDescription);
        }

        [Query]
        [None(typeof(CharacterTriggerAreaComponent), typeof(AvatarModifierAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupAvatarModifierArea(in Entity entity, ref PBAvatarModifierArea pbAvatarModifierArea)
        {
            World.Add(entity,
                new CharacterTriggerAreaComponent(areaSize: pbAvatarModifierArea.Area, targetOnlyMainPlayer: false),
                new AvatarModifierAreaComponent(pbAvatarModifierArea.ExcludeIds));
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateAvatarModifierArea(ref PBAvatarModifierArea pbAvatarModifierArea, ref AvatarModifierAreaComponent modifierAreaComponent, ref CharacterTriggerAreaComponent triggerAreaComponent)
        {
            if (pbAvatarModifierArea.IsDirty)
            {
                pbAvatarModifierArea.IsDirty = false;

                triggerAreaComponent.AreaSize = pbAvatarModifierArea.Area;
                triggerAreaComponent.IsDirty = true;

                modifierAreaComponent.SetExcludedIds(pbAvatarModifierArea.ExcludeIds);

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
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBAvatarModifierArea))]
        private void HandleComponentRemoval(ref CharacterTriggerAreaComponent triggerAreaComponent, ref AvatarModifierAreaComponent modifierComponent)
        {
            // Reset state of affected entities
            foreach (Transform avatarTransform in triggerAreaComponent.CurrentAvatarsInside) { ToggleAvatarHiding(avatarTransform, false, modifierComponent.ExcludedIds); }
        }

        internal void ToggleAvatarHiding(Transform avatarTransform, bool shouldHide, HashSet<string> excludedIds)
        {
            var found = false;

            // There's no way to do a Query/InlineQuery getting both entity and TransformComponent...
            globalWorld.Query(in AVATAR_BASE_QUERY,
                entity =>
                {
                    if (found) return;

                    Transform entityTransform = globalWorld.Get<AvatarBase>(entity).transform.parent;
                    if (avatarTransform != entityTransform) return;

                    found = true;

                    if (globalWorld.TryGet(entity, out Profile profile) && excludedIds.Contains(profile.UserId))
                        return;

                    globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea = shouldHide;
                });
        }

        internal void CorrectAvatarHidingState(Transform avatarTransform, HashSet<string> excludedIds)
        {
            var found = false;

            globalWorld.Query(in AVATAR_BASE_QUERY,
                entity =>
                {
                    if (found) return;

                    Transform entityTransform = globalWorld.Get<AvatarBase>(entity).transform.parent;
                    if (avatarTransform != entityTransform) return;

                    found = true;

                    if (!globalWorld.TryGet(entity, out Profile profile))
                        return;

                    globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea = !excludedIds.Contains(profile.UserId);
                });
        }

        [Query]
        private void FinalizeComponents(in Entity entity, ref CharacterTriggerAreaComponent triggerAreaComponent, ref AvatarModifierAreaComponent modifierComponent)
        {
            // Reset state of affected entities
            foreach (Transform avatarTransform in triggerAreaComponent.CurrentAvatarsInside) { ToggleAvatarHiding(avatarTransform, false, modifierComponent.ExcludedIds); }

            World.Remove<AvatarModifierAreaComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }
    }
}
