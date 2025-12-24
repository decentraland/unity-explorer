using DCL.UI;
using Utility;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LobbyAuthState : AuthStateBase
    {
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;

        public LobbyAuthState(AuthenticationScreenView viewInstance,
            AuthenticationScreenCharacterPreviewController characterPreviewController) : base(viewInstance)
        {
            this.characterPreviewController = characterPreviewController;
        }

        public override void Enter()
        {
            base.Enter();
            viewInstance!.FinalizeAnimator.ResetAnimator();
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
        }

        public override void Exit()
        {
            base.Exit();
        }
    }
}
