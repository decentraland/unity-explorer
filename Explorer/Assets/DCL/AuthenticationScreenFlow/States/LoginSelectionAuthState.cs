using DCL.Browser;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI;
using DCL.Utilities;
using DCL.Utility;
using DCL.Web3.Authenticators;
using MVC;
using System;
using System.Threading;
using UnityEngine.UI;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow
{
    public class LoginSelectionAuthState : AuthStateBase, IState, IPayloadedState<ErrorType>, IPayloadedState<int>
    {
        private const string REQUEST_BETA_ACCESS_LINK = "https://68zbqa0m12c.typeform.com/to/y9fZeNWm";

        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly LoginSelectionAuthView view;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly SplashScreen splashScreen;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;
        private readonly IWebBrowser webBrowser;
        private readonly bool enableEmailOTP;

        public LoginSelectionAuthState(MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState, SplashScreen splashScreen,
            ICompositeWeb3Provider compositeWeb3Provider, IWebBrowser webBrowser, bool enableEmailOTP) : base(viewInstance)
        {
            view = viewInstance.LoginSelectionAuthView;

            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.splashScreen = splashScreen;
            this.compositeWeb3Provider = compositeWeb3Provider;
            this.webBrowser = webBrowser;
            this.enableEmailOTP = enableEmailOTP;

            compositeWeb3Provider.OTPSendSuccess += OnOTPSendSuccess;

            // Cancel button persists in the Verification state (until code is shown)
            view.CancelLoginButton.onClick.AddListener(OnCancelBeforeVerification);
        }

        public new void Enter()
        {
            base.Enter();
            currentState.Value = AuthStatus.LoginSelectionScreen;

            view.SetLoadingSpinnerVisibility(false);
            view.SetEmailInputFieldSpinnerActive(false);

            if (view.gameObject.activeSelf)
            {
                // Listeners
                view.LoginMetamaskButton.onClick.AddListener(LoginWithMetamask);
                view.LoginGoogleButton.onClick.AddListener(LoginWithGoogle);
                view.LoginDiscordButton.onClick.AddListener(LoginWithDiscord);
                view.LoginAppleButton.onClick.AddListener(LoginWithApple);
                view.LoginXButton.onClick.AddListener(LoginWithX);
                view.LoginFortmaticButton.onClick.AddListener(LoginWithFormatic);
                view.LoginCoinbaseButton.onClick.AddListener(LoginWithCoinbase);
                view.LoginWalletConnectButton.onClick.AddListener(LoginWithWalletConnect);

                view.ErrorPopupRetryButton.onClick.AddListener(OnRetryFromError);
                view.ErrorPopupCloseButton.onClick.AddListener(CloseErrorPopup);
                view.ErrorPopupExitButton.onClick.AddListener(ExitUtils.Exit);

                view.RequestAlphaAccessButton.onClick.AddListener(RequestAlphaAccess);

                view.MoreOptionsButton.onClick.AddListener(view.ToggleOptionsPanelExpansion);

                foreach (Button button in view.UseAnotherAccountButton)
                    button.onClick.AddListener(controller.ChangeAccount);

                // ThirdWeb
                view.EmailInputField.Submitted += OTPLogin;
            }
        }

        public override void Exit()
        {
            view.RestrictedUserContainer.SetActive(false);
            view!.ErrorPopupRoot.SetActive(false);

            view.LoginMetamaskButton.onClick.RemoveAllListeners();
            view.LoginGoogleButton.onClick.RemoveAllListeners();
            view.LoginDiscordButton.onClick.RemoveAllListeners();
            view.LoginAppleButton.onClick.RemoveAllListeners();
            view.LoginXButton.onClick.RemoveAllListeners();
            view.LoginFortmaticButton.onClick.RemoveAllListeners();
            view.LoginCoinbaseButton.onClick.RemoveAllListeners();
            view.LoginWalletConnectButton.onClick.RemoveAllListeners();

            view.ErrorPopupRetryButton.onClick.RemoveAllListeners();
            view.ErrorPopupCloseButton.onClick.RemoveAllListeners();
            view.ErrorPopupExitButton.onClick.RemoveAllListeners();

            view.RequestAlphaAccessButton.onClick.RemoveAllListeners();

            view.MoreOptionsButton.onClick.RemoveAllListeners();

            foreach (Button button in view.UseAnotherAccountButton)
                button.onClick.RemoveAllListeners();

            // ThirdWeb
            view.EmailInputField.Submitted -= OTPLogin;
            base.Exit();
        }

        public void Enter(ErrorType errorType)
        {
            switch (errorType)
            {
                case ErrorType.NONE: break;
                case ErrorType.CONNECTION_ERROR:
                    view.ErrorPopupRoot.SetActive(true);
                    break;
                case ErrorType.RESTRICTED_USER:
                    view.RestrictedUserContainer.SetActive(true);
                    break;
                case ErrorType.INVALID_EMAIL:
                    view.SetEmailInputFieldErrorState(true);
                    Enter();
                    return;
                default: throw new ArgumentOutOfRangeException(nameof(errorType), errorType, null);
            }

            Enter(UIAnimationHashes.SLIDE);
        }

        public void Enter(int animHash)
        {
            if (splashScreen != null) // it can be destroyed after first login
                splashScreen.FadeOutAndHide();

            view.Show(animHash, moreOptionsExpanded: !enableEmailOTP);
            Enter();
        }

        // dApp endpoint is case insensitive

        private void LoginWithMetamask() =>
            Login(LoginMethod.METAMASK);

        private void LoginWithGoogle() =>
            Login(LoginMethod.GOOGLE);

        private void LoginWithDiscord() =>
            Login(LoginMethod.DISCORD);

        private void LoginWithApple() =>
            Login(LoginMethod.APPLE);

        private void LoginWithX() =>
            Login(LoginMethod.X);

        private void LoginWithFormatic() =>
            Login(LoginMethod.FORTMATIC);

        private void LoginWithCoinbase() =>
            Login(LoginMethod.COINBASE);

        private void LoginWithWalletConnect() =>
            Login(LoginMethod.WALLETCONNECT);

        private void Login(LoginMethod method)
        {
            compositeWeb3Provider.CurrentProvider = AuthProvider.Dapp;

            controller.CurrentLoginMethod = method;
            currentState.Value = AuthStatus.LoginRequested;

            view.SetLoadingSpinnerVisibility(true);
            machine.Enter<IdentityVerificationDappAuthState, (LoginMethod, CancellationToken)>((method, controller.GetRestartedLoginToken()));
        }

        private void OTPLogin()
        {
            compositeWeb3Provider.CurrentProvider = AuthProvider.ThirdWeb;

            controller.CurrentLoginMethod = LoginMethod.EMAIL_OTP;
            currentState.Value = AuthStatus.LoginRequested;
            view.SetEmailInputFieldSpinnerActive(true);

            machine.Enter<IdentityVerificationOTPAuthState, (string, CancellationToken)>(
                payload: (view.EmailInputField.Text, controller.GetRestartedLoginToken()));
        }

        private void OnOTPSendSuccess(string _)
        {
            view.SetEmailInputFieldSpinnerActive(false);
            view.Hide();
        }

        private void OnRetryFromError()
        {
            controller.CancelLoginProcess();
            machine.Enter<LoginSelectionAuthState>(true);
        }

        private void OnCancelBeforeVerification()
        {
            controller.CancelLoginProcess();
            machine.Enter<LoginSelectionAuthState>(true);
        }

        private void CloseErrorPopup() =>
            view.ErrorPopupRoot.SetActive(false);

        private void RequestAlphaAccess() =>
            webBrowser.OpenUrl(REQUEST_BETA_ACCESS_LINK);
    }

    public enum ErrorType
    {
        NONE = 0,
        CONNECTION_ERROR = 1,
        RESTRICTED_USER = 2,
        INVALID_EMAIL = 3,
    }
}
