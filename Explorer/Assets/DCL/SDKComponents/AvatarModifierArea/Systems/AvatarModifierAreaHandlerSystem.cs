using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
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
            Debug.Log("PRAVS - ENTERED AREA!");
        }

        internal void OnExitedAvatarModifierArea(Collider avatarCollider)
        {
            Debug.Log("PRAVS - EXITED AREA!");
        }
    }
}
