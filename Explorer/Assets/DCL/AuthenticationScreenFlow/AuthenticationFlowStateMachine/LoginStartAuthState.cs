using DCL.UI;
using DCL.Utilities;
using DCL.Utility;
using MVC;
using System;
using Utility;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoginStartAuthState : AuthStateBase, IPayloadedState<PopupType>
    {
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;

        public LoginStartAuthState(AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState) : base(viewInstance)
        {
            this.controller = controller;
            this.currentState = currentState;
        }

        public void Enter(PopupType payload)
        {
            Enter();

            switch (payload)
            {
                case PopupType.NONE: break;
                case PopupType.CONNECTION_ERROR:
                    viewInstance!.ErrorPopupRoot.SetActive(true);
                    break;
                case PopupType.RESTRICTED_USER:
                    viewInstance!.RestrictedUserContainer.SetActive(true);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(payload), payload, null);
            }
        }

        public override void Enter()
        {
            base.Enter();
            currentState.Value = AuthenticationStatus.Login;

            // GameObjects state setup
            viewInstance.VerificationContainer.SetActive(false);
            viewInstance.VerificationCodeHintContainer.SetActive(false);
            viewInstance.RestrictedUserContainer.SetActive(false);

            viewInstance.LoginAnimator.ResetAnimator();
            viewInstance.LoginContainer.SetActive(true);
            viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);

            viewInstance.LoginButton.interactable = true;
            viewInstance.LoginButton.gameObject.SetActive(true);

            viewInstance.LoadingSpinner.SetActive(false);

            // Listeners
            viewInstance.LoginButton.onClick.AddListener(StartLoginFlowUntilEnd);
            viewInstance.CancelLoginButton.onClick.AddListener(CancelLoginAndRestartFromBeginning);

            viewInstance.ErrorPopupCloseButton.onClick.AddListener(CloseErrorPopup);
            viewInstance.ErrorPopupExitButton.onClick.AddListener(ExitUtils.Exit);
            viewInstance.ErrorPopupRetryButton.onClick.AddListener(StartLoginFlowUntilEnd);
        }

        public override void Exit()
        {
            base.Exit();
            viewInstance.LoginButton.onClick.RemoveListener(StartLoginFlowUntilEnd);
            viewInstance.CancelLoginButton.onClick.RemoveListener(CancelLoginAndRestartFromBeginning);

            viewInstance.ErrorPopupCloseButton.onClick.RemoveListener(CloseErrorPopup);
            viewInstance.ErrorPopupExitButton.onClick.RemoveListener(ExitUtils.Exit);
            viewInstance.ErrorPopupRetryButton.onClick.RemoveListener(StartLoginFlowUntilEnd);
        }

        private void StartLoginFlowUntilEnd()
        {
            viewInstance!.ErrorPopupRoot.SetActive(false);
            viewInstance!.LoadingSpinner.SetActive(true);

            viewInstance.LoginButton.interactable = false;
            viewInstance.LoginButton.gameObject.SetActive(false);

            machine.Enter<VerificationAuthState>();
        }

        private void CancelLoginAndRestartFromBeginning()
        {
            controller.CancelLoginProcess();
            machine.Enter<LoginStartAuthState>(allowReEnterSameState: true);
        }

        private void CloseErrorPopup() =>
            viewInstance!.ErrorPopupRoot.SetActive(false);
    }

    public enum PopupType
    {
        NONE = 0,
        CONNECTION_ERROR = 1,
        RESTRICTED_USER = 2,
    }
}
