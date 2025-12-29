using DCL.UI;
using DCL.Utilities;
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

            viewInstance.LoginButton.onClick.AddListener(OnLoginButtonClicked);
            viewInstance.CancelLoginButton.onClick.AddListener(CancelLoginAndRestartFromBeginning);
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

        public override void Exit()
        {
            base.Exit();
            viewInstance.LoginButton.onClick.RemoveListener(OnLoginButtonClicked);
            viewInstance.CancelLoginButton.onClick.RemoveListener(CancelLoginAndRestartFromBeginning);
        }

        private void OnLoginButtonClicked()
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
            machine.Enter<LoginStartAuthState>(allowReEnterSameState: true);
        }
    }

    public enum PopupType
    {
        NONE = 0,
        CONNECTION_ERROR = 1,
        RESTRICTED_USER = 2,
    }
}
