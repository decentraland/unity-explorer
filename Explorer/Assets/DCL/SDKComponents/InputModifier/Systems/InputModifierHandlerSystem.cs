using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SDKComponents.InputModifier.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using SceneRunner.Scene;

namespace DCL.SDKComponents.PlayerInputMovement.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class InputModifierHandlerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
    {
        private readonly Entity playerEntity;
        private readonly World globalWorld;
        private readonly ISceneStateProvider sceneStateProvider;

        public InputModifierHandlerSystem(World world, World globalWorld, Entity playerEntity, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.playerEntity = playerEntity;
            this.sceneStateProvider = sceneStateProvider;
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            ApplyModifiersQuery(World);
        }

        private void ResetModifiersOnLeave()
        {
            ref InputModifierComponent inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
            inputModifier.DisableAll = false;
            inputModifier.DisableWalk = false;
            inputModifier.DisableJog = false;
            inputModifier.DisableRun = false;
            inputModifier.DisableJump = false;
            inputModifier.DisableEmote = false;
        }

        [Query]
        private void ApplyModifiers(in PBInputModifier pbInputModifier)
        {
            if (!sceneStateProvider.IsCurrent) return;
            if(pbInputModifier.ModeCase == PBInputModifier.ModeOneofCase.None) return;

            ref var inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
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

        public void OnSceneIsCurrentChanged(bool value)
        {
            if (!value)
                ResetModifiersOnLeave();
        }

        public void FinalizeComponents(in Query query)
        {
            ResetModifiersOnLeave();
        }
    }
}
