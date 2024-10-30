using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
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
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;

        private SceneRestrictionsAction lastBusMessageAction = SceneRestrictionsAction.REMOVED;

        public InputModifierHandlerSystem(World world, World globalWorld, Entity playerEntity, ISceneStateProvider sceneStateProvider, ISceneRestrictionBusController sceneRestrictionBusController) : base(world)
        {
            this.playerEntity = playerEntity;
            this.sceneStateProvider = sceneStateProvider;
            this.globalWorld = globalWorld;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
        }

        protected override void Update(float t)
        {
            ApplyModifiersQuery(World);
        }

        private void SendBusMessage(InputModifierComponent inputModifier)
        {
            SceneRestrictionsAction currentAction = inputModifier is { DisableAll: false, DisableWalk: false, DisableJog: false, DisableRun: false, DisableJump: false, DisableEmote: false } ? SceneRestrictionsAction.REMOVED : SceneRestrictionsAction.APPLIED;

            if (currentAction == lastBusMessageAction) return;

            MovementsBlockedRestriction busMessage = new MovementsBlockedRestriction
            {
                DisableAll = inputModifier.DisableAll,
                DisableWalk = inputModifier.DisableWalk,
                DisableJog = inputModifier.DisableJog,
                DisableRun = inputModifier.DisableRun,
                DisableJump = inputModifier.DisableJump,
                DisableEmote = inputModifier.DisableEmote,
                Action = currentAction,
            };

            sceneRestrictionBusController.PushSceneRestriction(busMessage);
            lastBusMessageAction = currentAction;
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

            SendBusMessage(inputModifier);
        }

        [Query]
        private void ApplyModifiers(in PBInputModifier pbInputModifier)
        {
            if (!sceneStateProvider.IsCurrent) return;
            if(pbInputModifier.ModeCase == PBInputModifier.ModeOneofCase.None) return;

            if (!pbInputModifier.IsDirty) return;

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

            SendBusMessage(inputModifier);
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
