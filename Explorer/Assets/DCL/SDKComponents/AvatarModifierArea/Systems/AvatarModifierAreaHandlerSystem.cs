using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterTriggerArea.Components;
using DCL.CharacterTriggerArea.Systems;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace DCL.SDKComponents.AvatarModifierArea.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateBefore(typeof(CharacterTriggerAreaHandlerSystem))]
    [LogCategory(ReportCategory.CAMERA_MODE_AREA)]
    [ThrottlingEnabled]
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
            World.Add(entity, new CharacterTriggerAreaComponent
            {
                AreaSize = pbAvatarModifierArea.Area,
                TargetOnlyMainPlayer = false,
                OnEnteredTrigger = OnEnteredAvatarModifierArea,
                OnExitedTrigger = OnExitedAvatarModifierArea,
                IsDirty = true,
            });
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateAvatarModifierArea(ref PBAvatarModifierArea pbAvatarModifierArea, ref CharacterTriggerAreaComponent characterTriggerAreaComponent)
        {
            if (!pbAvatarModifierArea.IsDirty) return;

            characterTriggerAreaComponent.OnEnteredTrigger = OnEnteredAvatarModifierArea;
            characterTriggerAreaComponent.OnExitedTrigger = OnExitedAvatarModifierArea;
            characterTriggerAreaComponent.AreaSize = pbAvatarModifierArea.Area;
            characterTriggerAreaComponent.IsDirty = true;
        }

        internal void OnEnteredAvatarModifierArea(Collider avatarCollider)
        {
            var found = false;

            // There's no way to do a Query/InlineQuery getting both entity and TransformComponent...
            globalWorld.Query(in AVATAR_BASE_QUERY,
                entity =>
                {
                    if (found) return;

                    Transform entityTransform = globalWorld.Get<AvatarBase>(entity).transform.parent;

                    if (avatarCollider.transform == entityTransform)
                    {
                        globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea = true;
                        found = true;
                    }
                });
        }

        internal void OnExitedAvatarModifierArea(Collider avatarCollider)
        {
            var found = false;

            // There's no way to do a Query/InlineQuery getting both entity and TransformComponent...
            globalWorld.Query(in AVATAR_BASE_QUERY,
                entity =>
                {
                    if (found) return;

                    Transform entityTransform = globalWorld.Get<AvatarBase>(entity).transform.parent;

                    if (avatarCollider.transform == entityTransform)
                    {
                        globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea = false;
                        found = true;
                    }
                });
        }
    }
}
