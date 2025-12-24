using DCL.UI;
using Utility;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class VerificationAuthState : AuthStateBase
    {
        private readonly AuthenticationScreenController controller;

        public VerificationAuthState(AuthenticationScreenView viewInstance, AuthenticationScreenController controller) : base(viewInstance)
        {
            this.controller = controller;
        }

        public override void Enter()
        {
            base.Enter();
            viewInstance.VerificationAnimator.ResetAndDeactivateAnimator();

            viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.OUT);
            viewInstance.LoadingSpinner.SetActive(false);
            viewInstance.LoginButton.interactable = false;
            viewInstance.LoginButton.gameObject.SetActive(true);
            viewInstance.VerificationContainer.SetActive(true);
            viewInstance.VerificationAnimator.SetTrigger(UIAnimationHashes.IN);
            viewInstance.FinalizeContainer.SetActive(false);
            viewInstance.VerificationCodeHintContainer.SetActive(false);
            viewInstance.RestrictedUserContainer.SetActive(false);

            viewInstance.CancelAuthenticationProcess.onClick.AddListener(controller.CancelLoginProcess);
            viewInstance.VerificationCodeHintButton.onClick.AddListener(controller.OpenOrCloseVerificationCodeHint);
        }

        public override void Exit()
        {
            base.Exit();
            viewInstance.CancelAuthenticationProcess.onClick.RemoveListener(controller.CancelLoginProcess);
            viewInstance.VerificationCodeHintButton.onClick.RemoveListener(controller.OpenOrCloseVerificationCodeHint);
        }
    }
}
