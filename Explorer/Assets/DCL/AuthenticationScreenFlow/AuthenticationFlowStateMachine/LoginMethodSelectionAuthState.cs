using DCL.UI;
using DCL.Utilities;
using Utility;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoginMethodSelectionAuthState : AuthStateBase
    {
        private readonly ReactiveProperty<AuthenticationScreenController.AuthenticationStatus> currentState;

        public LoginMethodSelectionAuthState(AuthenticationScreenView? viewInstance,
            ReactiveProperty<AuthenticationScreenController.AuthenticationStatus> currentState) : base(viewInstance)
        {
            this.currentState = currentState;
        }

        public override void Enter()
        {
            base.Enter();

            viewInstance!.LoginAnimator.ResetAndDeactivateAnimator();
            viewInstance.VerificationContainer.SetActive(false);

            viewInstance.LoginContainer.SetActive(true);
            viewInstance.LoadingSpinner.SetActive(false);
            viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);
            viewInstance.LoginButton.interactable = true;
            viewInstance.LoginButton.gameObject.SetActive(true);
            viewInstance.LoadingSpinner.SetActive(false);
            viewInstance.VerificationCodeHintContainer.SetActive(false);
            viewInstance.RestrictedUserContainer.SetActive(false);
            currentState.Value = AuthenticationScreenController.AuthenticationStatus.Login;
        }

        public override void Exit()
        {
            base.Exit();
        }
    }
}
