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

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoginSelectionAuthState : AuthStateBase, IState, IPayloadedState<PopupType>, IPayloadedState<int>
    {
        private const string REQUEST_BETA_ACCESS_LINK = "https://68zbqa0m12c.typeform.com/to/y9fZeNWm";

        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly LoginSelectionAuthView view;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly SplashScreen splashScreen;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;
        private readonly IWebBrowser webBrowser;

        private bool isSubscribed;

        public LoginSelectionAuthState(MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState, SplashScreen splashScreen,
            ICompositeWeb3Provider compositeWeb3Provider, IWebBrowser webBrowser) : base(viewInstance)
        {
            view = viewInstance.LoginSelectionAuthView;

            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.splashScreen = splashScreen;
            this.compositeWeb3Provider = compositeWeb3Provider;
            this.webBrowser = webBrowser;

            // Cancel button persists in the Verification state (until code is shown)
            view.CancelLoginButton.onClick.AddListener(OnLoginCanceled);
        }

        private void InternalEnter()
        {
            currentState.Value = AuthenticationStatus.Login;

            // it can be destroyed after first login
            if (splashScreen != null)
                splashScreen.FadeOutAndHide();

            // avoid double subscription
            if (!isSubscribed)
            {
                isSubscribed = true;

                foreach (Button button in view.UseAnotherAccountButton)
                    button.onClick.AddListener(controller.RestartLogin);

                view.LoginMetamaskButton.onClick.AddListener(LoginWithMetamask);
                view.LoginGoogleButton.onClick.AddListener(LoginWithGoogle);
                view.LoginDiscordButton.onClick.AddListener(LoginWithDiscord);
                view.LoginAppleButton.onClick.AddListener(LoginWithApple);
                view.LoginXButton.onClick.AddListener(LoginWithX);
                view.LoginFortmaticButton.onClick.AddListener(LoginWithFormatic);
                view.LoginCoinbaseButton.onClick.AddListener(LoginWithCoinbase);
                view.LoginWalletConnectButton.onClick.AddListener(LoginWithWalletConnect);

                view.ErrorPopupCloseButton.onClick.AddListener(CloseErrorPopup);
                view.ErrorPopupExitButton.onClick.AddListener(ExitUtils.Exit);
                // viewInstance.ErrorPopupRetryButton.onClick.AddListener(Login);

                view.RequestAlphaAccessButton.onClick.AddListener(RequestAlphaAccess);

                view.MoreOptionsButton.onClick.AddListener(view.ToggleOptionsPanelExpansion);

                // ThirdWeb
                view.EmailInputField.Submitted += OTPLogin;
            }
        }

        public void Enter()
        {
            InternalEnter();
            view.Show(UIAnimationHashes.EMPTY);
        }

        public void Enter(int animHash)
        {
            InternalEnter();
            view.Show(animHash);
        }

        public void Enter(PopupType type)
        {
            ShowPopup(type);
            InternalEnter();
            view.Show(UIAnimationHashes.SLIDE);

            return;

            void ShowPopup(PopupType type)
            {
                switch (type)
                {
                    case PopupType.NONE: break;
                    case PopupType.CONNECTION_ERROR:
                        view!.ErrorPopupRoot.SetActive(true);
                        break;
                    case PopupType.RESTRICTED_USER:
                        view!.RestrictedUserContainer.SetActive(true);
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(PopupType), type, null);
                }
            }
        }

        public override void Exit()
        {
            isSubscribed = false;

            view!.ErrorPopupRoot.SetActive(false);
            view.RestrictedUserContainer.SetActive(false);

            view.LoginMetamaskButton.onClick.RemoveAllListeners();
            view.LoginGoogleButton.onClick.RemoveAllListeners();
            view.LoginDiscordButton.onClick.RemoveAllListeners();
            view.LoginAppleButton.onClick.RemoveAllListeners();
            view.LoginXButton.onClick.RemoveAllListeners();
            view.LoginFortmaticButton.onClick.RemoveAllListeners();
            view.LoginCoinbaseButton.onClick.RemoveAllListeners();
            view.LoginWalletConnectButton.onClick.RemoveAllListeners();

            view.ErrorPopupCloseButton.onClick.RemoveAllListeners();
            view.ErrorPopupExitButton.onClick.RemoveAllListeners();
            // viewInstance.ErrorPopupRetryButton.onClick.RemoveAllListeners();

            view.RequestAlphaAccessButton.onClick.RemoveAllListeners();

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
            compositeWeb3Provider.CurrentMethod = AuthMethod.DappWallet;

            view.ShowLoading();
            machine.Enter<IdentityVerificationDappAuthState, LoginMethod>(method);
        }

        private void OTPLogin()
        {
            compositeWeb3Provider.CurrentMethod = AuthMethod.ThirdWebOTP;

            view.Hide();
            machine.Enter<IdentityVerificationOTPAuthState, string>(viewInstance.LoginSelectionAuthView.EmailInputField.Text);
        }

        private void OnLoginCanceled()
        {
            machine.Enter<LoginSelectionAuthState>(allowReEnterSameState: true);
        }

        private void CloseErrorPopup() =>
            view!.ErrorPopupRoot.SetActive(false);

        private void RequestAlphaAccess() =>
            webBrowser.OpenUrl(REQUEST_BETA_ACCESS_LINK);
    }

    public enum PopupType
    {
        NONE = 0,
        CONNECTION_ERROR = 1,
        RESTRICTED_USER = 2,
    }
}
