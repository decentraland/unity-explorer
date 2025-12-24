using DCL.UI;
using DCL.Utilities;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoadingAuthState : AuthStateBase
    {
        private readonly ReactiveProperty<AuthenticationStatus> currentState;

        public LoadingAuthState(AuthenticationScreenView viewInstance,
            ReactiveProperty<AuthenticationStatus> currentState) : base(viewInstance)
        {
            this.currentState = currentState;
        }

        public override void Enter()
        {
            base.Enter();
            currentState.Value = AuthenticationStatus.FetchingProfile;

            viewInstance.VerificationContainer.SetActive(false);
            viewInstance.LoginContainer.SetActive(true);
            viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);
            viewInstance.LoadingSpinner.SetActive(true);
            viewInstance.FinalizeContainer.SetActive(false);
            viewInstance.VerificationCodeHintContainer.SetActive(false);
            viewInstance.LoginButton.interactable = false;
            viewInstance.LoginButton.gameObject.SetActive(false);
            viewInstance.RestrictedUserContainer.SetActive(false);
        }

        public override void Exit()
        {
            base.Exit();
        }
    }
}
