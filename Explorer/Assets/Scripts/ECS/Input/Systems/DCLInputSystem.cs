using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components.Special;
using ECS.Abstract;
using ECS.CharacterMotion.Components;
using ECS.Input.Handler;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DCLInputSystem : BaseUnityLoopSystem
    {

        private  readonly List<InputComponentHandler> inputComponentHandlers;

        public DCLInputSystem(World world, DCLInput dclInput, List<InputComponentHandler> inputComponentHandlers) : base(world)
        {
            dclInput.Enable();
            this.inputComponentHandlers = inputComponentHandlers;
        }

        protected override void Update(float t)
        {
            foreach (InputComponentHandler inputComponentHandler in inputComponentHandlers)
            {
                World.Query(new QueryDescription().WithAll<PlayerComponent>(),
                    (in Entity entity) => inputComponentHandler.HandleInput(World, entity, t));
            }

            /*World.Query(new QueryDescription().WithAll<JumpInputComponent>(),
                (ref JumpInputComponent jumpComponent) => Debug.Log("AAA " + jumpComponent.Power));

            World.Query(new QueryDescription().WithAll<MovementInputComponent>(),
                (ref MovementInputComponent jumpComponent) =>
                {
                    Debug.Log("BBB " + jumpComponent.Axes);
                    Debug.Log("CCC " + jumpComponent.Kind);
                });*/
        }

    }

}
