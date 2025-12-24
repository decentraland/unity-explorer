using DCL.UI;
using DCL.Utilities;
using Utility;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoginStartAuthState : AuthStateBase
    {
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;

        public LoginStartAuthState(AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState) : base(viewInstance)
        {
            this.controller = controller;
            this.currentState = currentState;
        }

        public override void Enter()
        {
            base.Enter();
            currentState.Value = AuthenticationStatus.Login;

            viewInstance.VerificationContainer.SetActive(false);
            viewInstance.VerificationCodeHintContainer.SetActive(false);
            viewInstance.RestrictedUserContainer.SetActive(false);

            viewInstance.LoginAnimator.ResetAnimator();
            viewInstance.LoginContainer.SetActive(true);
            viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);

            viewInstance.LoginButton.interactable = true;
            viewInstance.LoginButton.gameObject.SetActive(true);

            viewInstance.LoadingSpinner.SetActive(false);

            viewInstance.LoginButton.onClick.AddListener(StartLoginProcess);
            viewInstance.CancelLoginButton.onClick.AddListener(CancelLoginAndRestartFromBeginning);
        }

        public override void Exit()
        {
            base.Exit();
            viewInstance.LoginButton.onClick.RemoveListener(controller.StartLoginFlowUntilEnd);
            viewInstance.CancelLoginButton.onClick.RemoveListener(CancelLoginAndRestartFromBeginning);
        }

        private void StartLoginProcess()
        {
            viewInstance!.ErrorPopupRoot.SetActive(false);

            viewInstance.LoginButton.interactable = false;
            viewInstance.LoginButton.gameObject.SetActive(false);

            viewInstance!.LoadingSpinner.SetActive(true);

            controller.StartLoginFlowUntilEnd();
        }

        private void CancelLoginAndRestartFromBeginning()
        {
            controller.CancelLoginProcess();
            Enter(); // SwitchState(ViewState.Login);
        }
    }
}
