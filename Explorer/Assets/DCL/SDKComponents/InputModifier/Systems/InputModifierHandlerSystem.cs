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

        protected override void Update(float t) =>
            ApplyModifiersQuery(World);

        [Query]
        private void ApplyModifiers(in PBInputModifier pbInputModifier)
        {
            if (!sceneStateProvider.IsCurrent) return;
            if (!pbInputModifier.IsDirty) return;

            DoApplyModifiers(pbInputModifier);
        }

        [Query]
        private void ForceApplyModifiers(ref PBInputModifier pbInputModifier) =>
            DoApplyModifiers(pbInputModifier);

        private void DoApplyModifiers(PBInputModifier pbInputModifier)
        {
            ref var inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);

            if (pbInputModifier.ModeCase == PBInputModifier.ModeOneofCase.None)
            {
                inputModifier.Clear();
                SendBusMessage(inputModifier);
                return;
            }

            PBInputModifier.Types.StandardInput? standardInput = pbInputModifier.Standard;

            bool disableAll = standardInput.DisableAll;
            inputModifier.DisableAll = disableAll;

            if (!disableAll)
            {
                inputModifier.DisableWalk = standardInput.DisableWalk;
                inputModifier.DisableJog = standardInput.DisableJog;
                inputModifier.DisableRun = standardInput.DisableRun;
                inputModifier.DisableJump = standardInput.DisableJump;
                inputModifier.DisableEmote = standardInput.DisableEmote;
                inputModifier.DisableDoubleJump = standardInput.DisableDoubleJump;
                inputModifier.DisableGliding = standardInput.DisableGliding;
            }
        }

        private void SendBusMessage(InputModifierComponent inputModifier)
        {
            SceneRestrictionsAction currentAction = inputModifier.EverythingEnabled ? SceneRestrictionsAction.REMOVED : SceneRestrictionsAction.APPLIED;

            if (currentAction == lastBusMessageAction) return;

            lastBusMessageAction = currentAction;

            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateAvatarMovementsBlocked(currentAction));
        }

        public void OnSceneIsCurrentChanged(bool sceneIsCurrent)
        {
            if (sceneIsCurrent)
                // If the scene became current, make sure the modifier is applied again by forcing it
                ForceApplyModifiersQuery(World);
            else
                // Otherwise reset it
                ClearModifierComponent();
        }

        private void ClearModifierComponent()
        {
            ref InputModifierComponent inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
            inputModifier.Clear();

            SendBusMessage(inputModifier);
        }

        public void FinalizeComponents(in Query query) =>
            ClearModifierComponent();
    }
}
