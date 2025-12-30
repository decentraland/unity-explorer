using DCL.UI;
using DCL.Utilities;
using Utility;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class VerificationAuthState : AuthStateBase
    {
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;

        public VerificationAuthState(AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState) : base(viewInstance)
        {
            this.controller = controller;
            this.currentState = currentState;
        }

        public override void Enter()
        {
            base.Enter();



            currentState.Value = AuthenticationStatus.VerificationInProgress;

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

            // Listeners
            viewInstance.CancelAuthenticationProcess.onClick.AddListener(controller.CancelLoginProcess);
            viewInstance.VerificationCodeHintButton.onClick.AddListener(ToggleVerificationCodeVisibility);
        }

        public override void Exit()
        {
            base.Exit();
            viewInstance.CancelAuthenticationProcess.onClick.RemoveListener(controller.CancelLoginProcess);
            viewInstance.VerificationCodeHintButton.onClick.RemoveListener(ToggleVerificationCodeVisibility);
        }



        private void ToggleVerificationCodeVisibility() =>
            viewInstance!.VerificationCodeHintContainer.SetActive(!viewInstance.VerificationCodeHintContainer.activeSelf);
    }
}
