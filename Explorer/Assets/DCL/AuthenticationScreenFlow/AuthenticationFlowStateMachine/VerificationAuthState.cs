using DCL.UI;
using Utility;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class VerificationAuthState : AuthStateBase
    {
        public VerificationAuthState(AuthenticationScreenView viewInstance) : base(viewInstance) { }

        public override void Enter()
        {
            base.Enter();
            viewInstance!.VerificationAnimator.ResetAndDeactivateAnimator();

            viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.OUT);
            viewInstance.LoadingSpinner.SetActive(false);
            viewInstance.LoginButton.interactable = false;
            viewInstance.LoginButton.gameObject.SetActive(true);
            viewInstance.VerificationContainer.SetActive(true);
            viewInstance.VerificationAnimator.SetTrigger(UIAnimationHashes.IN);
            viewInstance.FinalizeContainer.SetActive(false);
            viewInstance.VerificationCodeHintContainer.SetActive(false);
            viewInstance.RestrictedUserContainer.SetActive(false);
        }

        public override void Exit()
        {
            base.Exit();
        }
    }
}
