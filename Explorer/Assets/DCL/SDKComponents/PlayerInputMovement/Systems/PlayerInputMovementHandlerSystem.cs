using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.SDKComponents.PlayerInputMovement.Components;
using ECS.Abstract;
using ECS.LifeCycle;
using UnityEngine;

namespace DCL.SDKComponents.PlayerInputMovement.Systems
{
    //[UpdateBefore(input)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(CalculateCharacterVelocitySystem))]
    public partial class PlayerInputMovementHandlerSystem: BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        public PlayerInputMovementHandlerSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            //throw new NotImplementedException();
            ApplyModifiersQuery(World);
            testQuery(World);
        }

        [Query]
        [All(typeof(PlayerInputMovementComponent))]
        private void test()
        {
            var a = 2;// random code to add a breakpoint
        }

        [Query]
        private void ApplyModifiers(ref MovementInputComponent movementInput, in PlayerInputMovementComponent playerInputMovementComponent)
        {
            var a = 2;// random code to add a breakpoint
            if (playerInputMovementComponent.disable_all)
            {
                movementInput.Kind = MovementKind.None;
                movementInput.Axes = Vector2.zero;
                movementInput.AutoWalk = false;
            }
        }

        public void FinalizeComponents(in Query query)
        {
            //World.Remove<PlayerInputMovementComponent>(FinalizeComponents_QueryDescription);
            //throw new NotImplementedException();
        }
    }
}
