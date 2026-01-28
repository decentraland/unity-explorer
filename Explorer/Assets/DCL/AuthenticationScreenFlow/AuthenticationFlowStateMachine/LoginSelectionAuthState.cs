using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI;
using DCL.Utilities;
using DCL.Utility;
using DCL.Web3.Authenticators;
using MVC;
using System;
using System.Threading;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoginSelectionAuthState : AuthStateBase, IState, IPayloadedState<PopupType>, IPayloadedState<int>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly LoginSelectionAuthView view;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly SplashScreen splashScreen;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;

        public LoginSelectionAuthState(MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState, SplashScreen splashScreen,
            ICompositeWeb3Provider compositeWeb3Provider) : base(viewInstance)
        {
            view = viewInstance.LoginSelectionAuthView;

            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.splashScreen = splashScreen;
            this.compositeWeb3Provider = compositeWeb3Provider;

            // Cancel button persists in the Verification state (until code is shown)
            view.CancelLoginButton.onClick.AddListener(OnCancelBeforeVerification);
        }

        public void Enter()
        {
            view.SetLoadingSpinnerVisibility(false);

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

                viewInstance.ErrorPopupRetryButton.onClick.AddListener(OnRetryFromError);
                viewInstance.ErrorPopupCloseButton.onClick.AddListener(CloseErrorPopup);
                viewInstance.ErrorPopupExitButton.onClick.AddListener(ExitUtils.Exit);

                view.MoreOptionsButton.onClick.AddListener(view.ToggleOptionsPanelExpansion);

                // ThirdWeb
                view.EmailInputField.Submitted += OTPLogin;
            }
        }

        public void Enter(PopupType popupType)
        {
            switch (popupType)
            {
                case PopupType.NONE: break;
                case PopupType.CONNECTION_ERROR:
                    viewInstance!.ErrorPopupRoot.SetActive(true);
                    break;
                case PopupType.RESTRICTED_USER:
                    viewInstance!.RestrictedUserContainer.SetActive(true);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(popupType), popupType, null);
            }

            Enter(UIAnimationHashes.SLIDE);
        }

        public void Enter(int animHash)
        {
            if (splashScreen != null) // it can be destroyed after first login
                splashScreen.FadeOutAndHide();

            view.Show(animHash);
            Enter();
        }

        public override void Exit()
        {
            viewInstance.RestrictedUserContainer.SetActive(false);
            viewInstance!.ErrorPopupRoot.SetActive(false);

            view.LoginMetamaskButton.onClick.RemoveAllListeners();
            view.LoginGoogleButton.onClick.RemoveAllListeners();
            view.LoginDiscordButton.onClick.RemoveAllListeners();
            view.LoginAppleButton.onClick.RemoveAllListeners();
            view.LoginXButton.onClick.RemoveAllListeners();
            view.LoginFortmaticButton.onClick.RemoveAllListeners();
            view.LoginCoinbaseButton.onClick.RemoveAllListeners();
            view.LoginWalletConnectButton.onClick.RemoveAllListeners();

            viewInstance.ErrorPopupRetryButton.onClick.RemoveAllListeners();
            viewInstance.ErrorPopupCloseButton.onClick.RemoveAllListeners();
            viewInstance.ErrorPopupExitButton.onClick.RemoveAllListeners();

            view.MoreOptionsButton.onClick.RemoveAllListeners();

            // ThirdWeb
            view.EmailInputField.Submitted -= OTPLogin;
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
            controller.CurrentLoginMethod = method;
            compositeWeb3Provider.CurrentProvider = AuthProvider.Dapp;
            currentState.Value = AuthStatus.LoginRequested;

            view.SetLoadingSpinnerVisibility(true);
            machine.Enter<IdentityVerificationDappAuthState, (LoginMethod, CancellationToken)>((method, controller.GetRestartedLoginToken()));
        }

        private void OTPLogin()
        {
            controller.CurrentLoginMethod = LoginMethod.EMAIL_OTP;
            compositeWeb3Provider.CurrentProvider = AuthProvider.ThirdWeb;
            currentState.Value = AuthStatus.LoginRequested;

            view.Hide();
            machine.Enter<IdentityVerificationOTPAuthState, (string, CancellationToken)>(
                payload: (viewInstance.LoginSelectionAuthView.EmailInputField.Text, controller.GetRestartedLoginToken()));
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
            viewInstance!.ErrorPopupRoot.SetActive(false);
    }

    public enum PopupType
    {
        NONE = 0,
        CONNECTION_ERROR = 1,
        RESTRICTED_USER = 2,
    }
}
