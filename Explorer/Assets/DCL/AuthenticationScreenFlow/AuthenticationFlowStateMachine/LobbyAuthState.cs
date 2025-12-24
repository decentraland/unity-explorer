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
            this.characterPreviewController = characterPreviewController;
            this.controller = controller;
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

            viewInstance.JumpIntoWorldButton.onClick.AddListener(controller.JumpIntoWorld);
        }

        public override void Exit()
        {
            base.Exit();
            viewInstance.JumpIntoWorldButton.onClick.RemoveListener(controller.JumpIntoWorld);
        }
    }
}
