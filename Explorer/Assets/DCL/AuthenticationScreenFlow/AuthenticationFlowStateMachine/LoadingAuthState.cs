using DCL.UI;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoadingAuthState : AuthStateBase
    {
        public LoadingAuthState(AuthenticationScreenView viewInstance) : base(viewInstance) { }

        public override void Enter()
        {
            base.Enter();
            viewInstance!.VerificationContainer.SetActive(false);
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
