using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.DemoScripts.Components;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Utils;
using DCL.Time.Systems;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(UpdatePhysicsTickSystem))]
    [UpdateBefore(typeof(CalculateCharacterVelocitySystem))]
    public partial class CalculateSpeedLimitSystem : BaseUnityLoopSystem
    {
        public CalculateSpeedLimitSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            ComputeLocalAvatarSpeedLimitQuery(World);
            ComputeRandomAvatarSpeedLimitQuery(World);
        }

        [Query]
        [None(typeof(RandomAvatar))]
        private void ComputeLocalAvatarSpeedLimit(in ICharacterControllerSettings settings, in MovementInputComponent movementInput, in GlideState glideState, ref MovementSpeedLimit speedLimit)
        {
            if (glideState.IsGliding)
            {
                speedLimit.Value = settings.GlideSpeed;
                return;
            }

            speedLimit.Value = MovementSpeedLimitHelper.GetMovementSpeedLimit(settings, movementInput.Kind);
        }

        [Query]
        [All(typeof(RandomAvatar))]
        private void ComputeRandomAvatarSpeedLimit(in ICharacterControllerSettings settings, in MovementInputComponent movementInput, ref MovementSpeedLimit speedLimit) =>
            speedLimit.Value = MovementSpeedLimitHelper.GetMovementSpeedLimit(settings, movementInput.Kind);
    }
}
