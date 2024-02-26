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
        private static readonly QueryDescription ENTITY_DESTRUCTION_QUERY = new QueryDescription().WithAll<DeleteEntityIntention, AvatarModifierAreaComponent>();
        private static readonly QueryDescription COMPONENT_REMOVAL_QUERY = new QueryDescription().WithAll<AvatarModifierAreaComponent>().WithNone<DeleteEntityIntention, PBAvatarModifierArea>();
        private static readonly QueryDescription AVATAR_BASE_QUERY = new QueryDescription().WithAll<AvatarBase>();
        private readonly World globalWorld;

        public AvatarModifierAreaHandlerSystem(World world, WorldProxy globalWorldProxy) : base(world)
        {
            globalWorld = globalWorldProxy.World;
        }

        protected override void Update(float t)
        {
            UpdateAvatarModifierAreaQuery(World);
            SetupAvatarModifierAreaQuery(World);

            World.Remove<AvatarModifierAreaComponent>(COMPONENT_REMOVAL_QUERY);
            World.Remove<AvatarModifierAreaComponent, PBAvatarModifierArea>(ENTITY_DESTRUCTION_QUERY);
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
            foreach (Transform avatarTransform in triggerAreaComponent.EnteredThisFrame) { ToggleAvatarHiding(avatarTransform, modifierAreaComponent.ExcludedIds, true); }

            foreach (Transform avatarTransform in triggerAreaComponent.ExitedThisFrame) { ToggleAvatarHiding(avatarTransform, modifierAreaComponent.ExcludedIds, false); }

            if (pbAvatarModifierArea.IsDirty)
            {
                triggerAreaComponent.IsDirty = true;
                triggerAreaComponent.AreaSize = pbAvatarModifierArea.Area;

                modifierAreaComponent.SetExcludedIds(pbAvatarModifierArea.ExcludeIds);
            }
        }

        internal void ToggleAvatarHiding(Transform avatarTransform, HashSet<string> excludedIds, bool shouldHide)
        {
            var found = false;

            // There's no way to do a Query/InlineQuery getting both entity and TransformComponent...
            globalWorld.Query(in AVATAR_BASE_QUERY,
                entity =>
                {
                    if (found) return;

                    Transform entityTransform = globalWorld.Get<AvatarBase>(entity).transform.parent;

                    if (avatarTransform == entityTransform)
                    {
                        found = true;

                        if (globalWorld.TryGet(entity, out Profile profile) && excludedIds.Contains(profile.UserId))
                            return;

                        globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea = shouldHide;
                    }
                });
        }

        [Query]
        [All(typeof(AvatarModifierAreaComponent))]
        private void FinalizeComponents(in Entity entity)
        {
            World.Remove<AvatarModifierAreaComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }
    }
}
