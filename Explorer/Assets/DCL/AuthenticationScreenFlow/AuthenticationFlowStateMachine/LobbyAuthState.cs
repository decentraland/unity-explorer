using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
using DCL.UI;
using Utility;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LobbyAuthState : AuthStateBase
    {
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly IInputBlock inputBlock;
        private readonly AuthenticationScreenController controller;

        public LobbyAuthState(AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            AuthenticationScreenCharacterPreviewController characterPreviewController, IInputBlock inputBlock) : base(viewInstance)
        {
            this.controller = controller;
            this.characterPreviewController = characterPreviewController;
            this.inputBlock = inputBlock;
        }

        public override void Enter()
        {
            base.Enter();
            viewInstance.FinalizeAnimator.ResetAnimator();
            viewInstance.VerificationContainer.SetActive(false);

            viewInstance.LoginContainer.SetActive(false);
            viewInstance.LoadingSpinner.SetActive(false);
            viewInstance.LoginButton.interactable = false;
            viewInstance.LoginButton.gameObject.SetActive(true);

            viewInstance.FinalizeContainer.SetActive(true);
            viewInstance.FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);
            viewInstance.VerificationCodeHintContainer.SetActive(false);
            viewInstance.RestrictedUserContainer.SetActive(false);
            viewInstance.JumpIntoWorldButton.interactable = true;
            characterPreviewController?.OnBeforeShow();
            characterPreviewController?.OnShow();

            viewInstance.JumpIntoWorldButton.onClick.AddListener(JumpIntoWorld);
        }

        public override void Exit()
        {
            base.Exit();
            viewInstance.JumpIntoWorldButton.onClick.RemoveListener(JumpIntoWorld);
        }

        private void JumpIntoWorld()
        {
            viewInstance!.JumpIntoWorldButton.interactable = false;
            AnimateAndAwaitAsync().Forget();
            return;

            async UniTaskVoid AnimateAndAwaitAsync()
            {
                await (characterPreviewController?.PlayJumpInEmoteAndAwaitItAsync() ?? UniTask.CompletedTask);

                //Disabled animation until proper animation is setup, otherwise we get animation hash errors
                //viewInstance!.FinalizeAnimator.SetTrigger(UIAnimationHashes.JUMP_IN);
                await UniTask.Delay(AuthenticationScreenController.ANIMATION_DELAY);
                characterPreviewController?.OnHide();

                // Restore inputs before transitioning to world
                UnblockUnwantedInputs();

                controller.lifeCycleTask?.TrySetResult();
                controller.lifeCycleTask = null;
            }
        }

        private void UnblockUnwantedInputs() =>
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
    }
}
