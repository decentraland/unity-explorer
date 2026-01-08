using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI;
using DCL.Utilities;
using DCL.Utility;
using DCL.Web3.Authenticators;
using MVC;
using System;
using System.Threading;
using Utility;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoginStartAuthState : AuthStateBase, IState, IPayloadedState<PopupType>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly SplashScreen splashScreen;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;

        public LoginStartAuthState(MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState, SplashScreen splashScreen,
            ICompositeWeb3Provider compositeWeb3Provider) : base(viewInstance)
        {
            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.splashScreen = splashScreen;
            this.compositeWeb3Provider = compositeWeb3Provider;

            // Cancel button persists in the Verification state (until code is shown)
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

        public void Enter()
        {
            if (machine.PreviousState is InitAuthScreenState)
                splashScreen.Hide();

            currentState.Value = AuthenticationStatus.Login;

            // GameObjects state setup
            viewInstance.LoginContainer.SetActive(true);

            viewInstance.LoginAnimator.ResetAnimator(); // Animator should be updated after enabling its gameObject
            viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);

            viewInstance.LoginButton.gameObject.SetActive(true);
            viewInstance.LoginButton.interactable = true;

            viewInstance.LoadingSpinner.SetActive(false);

            // Listeners
            viewInstance.LoginButton.onClick.AddListener(Login);

            viewInstance.ErrorPopupCloseButton.onClick.AddListener(CloseErrorPopup);
            viewInstance.ErrorPopupExitButton.onClick.AddListener(ExitUtils.Exit);
            viewInstance.ErrorPopupRetryButton.onClick.AddListener(Login);

            // ThirdWeb
            viewInstance.LoginWithOtpButton.onClick.AddListener(OTPLogin);
        }

        public override void Exit()
        {
            viewInstance.RestrictedUserContainer.SetActive(false);
            viewInstance!.ErrorPopupRoot.SetActive(false);

            viewInstance.LoginButton.onClick.RemoveListener(Login);

            viewInstance.ErrorPopupCloseButton.onClick.RemoveListener(CloseErrorPopup);
            viewInstance.ErrorPopupExitButton.onClick.RemoveListener(ExitUtils.Exit);
            viewInstance.ErrorPopupRetryButton.onClick.RemoveListener(Login);

            // ThirdWeb
            viewInstance.LoginWithOtpButton.onClick.RemoveListener(OTPLogin);
        }

        private void Login()
        {
            compositeWeb3Provider.CurrentMethod = AuthMethod.DappWallet;

            viewInstance.LoginButton.gameObject.SetActive(false);
            viewInstance.LoginButton.interactable = false;
            viewInstance!.LoadingSpinner.SetActive(true);

            machine.Enter<IdentityAndVerificationAuthState, CancellationToken>(controller.GetRestartedLoginToken());
        }

        private void OTPLogin()
        {
            compositeWeb3Provider.CurrentMethod = AuthMethod.ThirdWebOTP;

            // Anim-OUT non-interactable Login Screen
            viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.OUT);

            viewInstance.LoginButton.gameObject.SetActive(true);
            viewInstance.LoginButton.interactable = false;
            viewInstance.LoadingSpinner.SetActive(false);

            machine.Enter<IdentityAndOTPConfirmationState, (string, CancellationToken)>(
                payload: (viewInstance.EmailInputField.text, controller.GetRestartedLoginToken()));
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
