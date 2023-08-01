using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.CharacterMotion.Components;
using ECS.Input.Component;
using ECS.Input.Component.Physics;
using ECS.Input.Systems.Physics;

namespace ECS.Input.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(UpdateInputPhysicsTickSystem))]
    public partial class UpdateInputJumpSystem : BaseUnityLoopSystem
    {
        internal static readonly float AIR_TIME = 1.5f;

        private float currentAirTime;
        private bool isJumping;
        internal int tickValue;

        public UpdateInputJumpSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            GetTickValueQuery(World);
            UpdateInputQuery(World);

            if (isJumping)
                currentAirTime += t;
        }

        [Query]
        private void GetTickValue(ref PhysicsTickComponent physicsTickComponent)
        {
            tickValue = physicsTickComponent.tick;
        }

        [Query]
        private void UpdateInput(ref JumpInputComponent inputToUpdate)
        {
            //TODO: We need to add ground check, if not we could jump again while in the air
            if (inputToUpdate.IsKeyDown(tickValue) && !isJumping)
                isJumping = true;

            if (inputToUpdate.IsKeyUp(tickValue) || currentAirTime > AIR_TIME)
            {
                isJumping = false;
                currentAirTime = 0;
            }

            inputToUpdate.Power = isJumping ? 1 : 0;
        }
    }
}
