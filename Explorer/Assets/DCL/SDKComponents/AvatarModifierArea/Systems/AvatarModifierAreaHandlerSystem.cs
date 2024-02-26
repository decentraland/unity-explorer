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
using DCL.Utilities;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace DCL.SDKComponents.AvatarModifierArea.Systems
{
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    [UpdateBefore(typeof(CharacterTriggerAreaCleanupSystem))]
    [LogCategory(ReportCategory.CAMERA_MODE_AREA)]
    public partial class AvatarModifierAreaHandlerSystem : BaseUnityLoopSystem
    {
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
        }

        [Query]
        [None(typeof(CharacterTriggerAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupAvatarModifierArea(in Entity entity, ref PBAvatarModifierArea pbAvatarModifierArea)
        {
            World.Add(entity, new CharacterTriggerAreaComponent(areaSize: pbAvatarModifierArea.Area, targetOnlyMainPlayer: false));
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateAvatarModifierArea(ref PBAvatarModifierArea pbAvatarModifierArea, ref CharacterTriggerAreaComponent characterTriggerAreaComponent)
        {
            if (characterTriggerAreaComponent.EnteredThisFrame!.Count > 0)
                foreach (Transform avatarTransform in characterTriggerAreaComponent.EnteredThisFrame) { OnEnteredAvatarModifierArea(avatarTransform); }

            if (characterTriggerAreaComponent.ExitedThisFrame!.Count > 0)
                foreach (Transform avatarTransform in characterTriggerAreaComponent.ExitedThisFrame) { OnExitedAvatarModifierArea(avatarTransform); }

            if (pbAvatarModifierArea.IsDirty)
            {
                characterTriggerAreaComponent.AreaSize = pbAvatarModifierArea.Area;
                characterTriggerAreaComponent.IsDirty = true;
            }
        }

        internal void OnEnteredAvatarModifierArea(Transform avatarTransform)
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
                        globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea = true;
                        found = true;
                    }
                });
        }

        internal void OnExitedAvatarModifierArea(Transform avatarTransform)
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
                        globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea = false;
                        found = true;
                    }
                });
        }
    }
}
