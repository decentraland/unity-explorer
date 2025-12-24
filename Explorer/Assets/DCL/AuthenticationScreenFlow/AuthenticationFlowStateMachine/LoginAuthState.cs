using DCL.UI;
using Utility;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoginAuthState : AuthStateBase
    {
        private readonly AuthenticationScreenController controller;

        public LoginAuthState(AuthenticationScreenView viewInstance, AuthenticationScreenController controller) : base(viewInstance)
        {
            this.controller = controller;
        }

        public override void Enter()
        {
            base.Enter();
            viewInstance.LoginAnimator.ResetAnimator();
            viewInstance.VerificationContainer.SetActive(false);
            viewInstance.LoginContainer.SetActive(true);
            viewInstance.LoadingSpinner.SetActive(false);
            viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);
            viewInstance.LoginButton.interactable = true;
            viewInstance.LoginButton.gameObject.SetActive(true);
            viewInstance.LoadingSpinner.SetActive(false);
            viewInstance.VerificationCodeHintContainer.SetActive(false);
            viewInstance.RestrictedUserContainer.SetActive(false);

            viewInstance.LoginButton.onClick.AddListener(controller.StartLoginFlowUntilEnd);
            viewInstance.CancelLoginButton.onClick.AddListener(CancelLoginAndRestartFromBeginning);
        }

        public override void Exit()
        {
            base.Exit();
            viewInstance.LoginButton.onClick.RemoveListener(controller.StartLoginFlowUntilEnd);
            viewInstance.CancelLoginButton.onClick.RemoveListener(CancelLoginAndRestartFromBeginning);
        }

        private void CancelLoginAndRestartFromBeginning()
        {
            controller.CancelLoginProcess();
            Enter(); // SwitchState(ViewState.Login);
        }
    }
}
