using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Systems;
using DCL.ECSComponents;
using DCL.Input;
using DCL.SDKComponents.InputModifier.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle;
using SceneRunner.Scene;

namespace DCL.SDKComponents.PlayerInputMovement.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateAfter(typeof(UpdateInputMovementSystem))]
    public partial class InputModifierHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly ObjectProxy<Entity> playerEntity;
        private readonly World globalWorld;
        private readonly ISceneStateProvider sceneStateProvider;

        public InputModifierHandlerSystem(World world, ObjectProxy<World> globalWorldProxy, ObjectProxy<Entity> playerEntity, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.playerEntity = playerEntity;
            this.sceneStateProvider = sceneStateProvider;
            globalWorld = globalWorldProxy.Object;
        }

        protected override void Update(float t)
        {
            ApplyModifiersQuery(World);
            ApplyModifiers2Query(World);
            ResetModifiersOnLeaveQuery(World);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void ResetModifiersOnLeave()
        {
            if (sceneStateProvider.IsCurrent) return;

            ref InputModifierComponent inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity.StrictObject);
            inputModifier.DisableAll = false;
            inputModifier.DisableWalk = false;
            inputModifier.DisableJog = false;
            inputModifier.DisableRun = false;
            inputModifier.DisableJump = false;
            inputModifier.DisableEmote = false;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        void ApplyModifiers(in PBInputModifier pbInputModifier)
        {
            if (!sceneStateProvider.IsCurrent) return;

            ref var inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity.StrictObject);
            PBInputModifier.Types.StandardInput? pb = pbInputModifier.Standard;

            bool disableAll = pb.DisableAll;
            inputModifier.DisableAll = disableAll;

            if (!disableAll)
            {
                inputModifier.DisableWalk = pb.DisableWalk;
                inputModifier.DisableJog = pb.DisableJog;
                inputModifier.DisableRun = pb.DisableRun;
                inputModifier.DisableJump = pb.DisableJump;
                inputModifier.DisableEmote = pb.DisableEmote;
            }
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void ApplyModifiers2(in PBInputModifier pbInputModifier, ref InputModifierComponent inputModifier)
        {
            var a = 2; // TODO remove random code used to add a breakpoint

            //     //Debug.Log($"{UnityEngine.Time.frameCount} IG- ApplyModifiers Axes: {movementInput.Axes} {movementInput.Kind}");
            //
            //     PBInputModifier.Types.StandardInput? pb = pbInputModifier.Standard;
            //     inputModifier.DisableAll = pb.DisableAll;
            //     inputModifier.DisableWalk = pb.DisableWalk;
            //     inputModifier.DisableJog = pb.DisableJog;
            //     inputModifier.DisableRun = pb.DisableRun;
            //     inputModifier.DisableJump = pb.DisableJump;
            //     inputModifier.DisableEmote = pb.DisableEmote;
        }

        public void FinalizeComponents(in Query query)
        {
            // Ignore for now
        }
    }
}
