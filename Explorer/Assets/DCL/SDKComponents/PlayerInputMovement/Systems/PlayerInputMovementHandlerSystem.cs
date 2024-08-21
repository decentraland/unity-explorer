using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.ECSComponents;
using DCL.SDKComponents.PlayerInputMovement.Components;
using ECS.Abstract;
using ECS.LifeCycle;
using UnityEngine;

namespace DCL.SDKComponents.PlayerInputMovement.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
   // [UpdateBefore(typeof(CalculateCharacterVelocitySystem))]
    public partial class PlayerInputMovementHandlerSystem: BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        public PlayerInputMovementHandlerSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            //throw new NotImplementedException();
            ApplyModifiersQuery(World);
            ApplyModifiers2Query(World);
        }

        [Query]
        private void ApplyModifiers2(ref MovementInputComponent movementInput,in PBPlayerInputMovement playerInputMovement){
            if(playerInputMovement.Standard.DisableAll)
            {
                movementInput.Kind = MovementKind.None;
                movementInput.Axes = Vector2.zero;
                movementInput.AutoWalk = false;
            }
        }

        [Query]
        private void ApplyModifiers(in PBPlayerInputMovement playerInputMovementComponent)
        {
            if(playerInputMovementComponent.Standard.DisableAll)
            {
                    // movementInput.Kind = MovementKind.None;
                    // movementInput.Axes = Vector2.zero;
                    // movementInput.AutoWalk = false;
            }
            // if (playerInputMovementComponent.disable_all)
            // {
            //     movementInput.Kind = MovementKind.None;
            //     movementInput.Axes = Vector2.zero;
            //     movementInput.AutoWalk = false;
            // }
        }

        public void FinalizeComponents(in Query query)
        {
            //World.Remove<PlayerInputMovementComponent>(FinalizeComponents_QueryDescription);
            //throw new NotImplementedException();
        }
    }
}
