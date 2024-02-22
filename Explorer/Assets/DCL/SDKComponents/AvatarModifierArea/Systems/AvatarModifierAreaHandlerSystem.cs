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
        private void SetupAvatarModifierArea(in Entity entity, ref PBAvatarModifierArea pbAvatarModifierArea) { }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateAvatarModifierArea(in Entity entity, ref PBAvatarModifierArea pbAvatarModifierArea, ref CharacterTriggerAreaComponent characterTriggerAreaComponent) { }

        internal void OnEnteredAvatarModifierArea(Collider avatarCollider) { }

        internal void OnExitedAvatarModifierArea(Collider avatarCollider) { }
    }
}
