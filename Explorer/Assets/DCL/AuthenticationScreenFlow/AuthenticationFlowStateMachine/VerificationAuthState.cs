using DCL.UI;
using UnityEngine;
using Utility;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class VerificationAuthState : AuthStateBase
    {
        public VerificationAuthState(AuthenticationScreenView? viewInstance) : base(viewInstance) { }

        private static void ResetAnimator(Animator animator)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.gameObject.SetActive(false);
        }

        public override void Enter()
        {
            base.Enter();

            viewInstance!.VerificationAnimator.ResetAnimator();

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
