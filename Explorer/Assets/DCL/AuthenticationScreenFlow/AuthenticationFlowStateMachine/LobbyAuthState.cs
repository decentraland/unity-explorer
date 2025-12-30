using Cysharp.Threading.Tasks;
using DCL.UI;
using Utility;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LobbyAuthState : AuthStateBase
    {
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly AuthenticationScreenController controller;

        public LobbyAuthState(AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            AuthenticationScreenCharacterPreviewController characterPreviewController) : base(viewInstance)
        {
            this.controller = controller;
            this.characterPreviewController = characterPreviewController;
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
            viewInstance!.FinalizeContainer.SetActive(false);
            characterPreviewController?.OnHide();

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

                controller.lifeCycleTask?.TrySetResult();
                controller.lifeCycleTask = null;
            }
        }
    }
}
