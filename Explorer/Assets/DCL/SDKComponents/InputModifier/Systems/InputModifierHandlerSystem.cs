using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle;

namespace DCL.SDKComponents.PlayerInputMovement.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateAfter(typeof(UpdateInputMovementSystem))]
    public partial class InputModifierHandlerSystem: BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly ObjectProxy<Entity> playerEntity;
        private readonly World globalWorld;

        public InputModifierHandlerSystem(World world,ObjectProxy<World> globalWorldProxy, ObjectProxy<Entity> playerEntity) : base(world)
        {
            this.playerEntity = playerEntity;
            this.globalWorld = globalWorldProxy.Object;
        }

        protected override void Update(float t)
        {
            ApplyModifiersQuery(World);
        }

        [Query]
        private void ApplyModifiers(in PBPlayerInputMovement pbInputModifier, ref InputModifierComponent inputModifier)
        {
            //Debug.Log($"{UnityEngine.Time.frameCount} IG- ApplyModifiers Axes: {movementInput.Axes} {movementInput.Kind}");

            var pb = pbInputModifier.Standard;
            inputModifier.DisableAll = pb.DisableAll;
            inputModifier.DisableWalk = pb.DisableWalk;
            inputModifier.DisableJog = pb.DisableJog;
            inputModifier.DisableRun = pb.DisableRun;
            inputModifier.DisableJump = pb.DisableJump;
            inputModifier.DisableCamera = pb.DisableCamera;
            inputModifier.DisableEmote = pb.DisableEmote;
        }

        public void FinalizeComponents(in Query query)
        {
            //World.Remove<PlayerInputMovementComponent>(FinalizeComponents_QueryDescription);
            //throw new NotImplementedException();
        }
    }
}
