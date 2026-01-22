using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI;
using DCL.Utilities;
using DCL.Utility;
using DCL.Web3.Authenticators;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoginSelectionAuthState : AuthStateBase, IPayloadedState<(PopupType type, int animHash)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly LoginSelectionAuthView view;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly SplashScreen splashScreen;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;

        public LoginSelectionAuthState(MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState, SplashScreen splashScreen,
            ICompositeWeb3Provider compositeWeb3Provider) : base(viewInstance)
        {
            view = viewInstance.LoginSelectionAuthView;

            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.splashScreen = splashScreen;
            this.compositeWeb3Provider = compositeWeb3Provider;

            // Cancel button persists in the Verification state (until code is shown)
            view.CancelLoginButton.onClick.AddListener(CancelLoginAndRestartFromBeginning);
        }

        private bool isFirstRun = true;

        public void Enter((PopupType type, int animHash) payload)
        {
            Debug.Log($"VVV {payload.animHash}");

            switch (payload.type)
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

            if (isFirstRun)
            {
                isFirstRun = false;
                splashScreen.FadeOutAndHide();
            }

            currentState.Value = AuthenticationStatus.Login;
            view.Show(payload.animHash);

            if (view.gameObject.activeSelf)
            {
                // Listeners
                view.MetamaskLoginButton.onClick.AddListener(LoginWithMetamask);
                view.GoogleLoginButton.onClick.AddListener(LoginWithGoogle);

                viewInstance.ErrorPopupCloseButton.onClick.AddListener(CloseErrorPopup);
                viewInstance.ErrorPopupExitButton.onClick.AddListener(ExitUtils.Exit);

                // viewInstance.ErrorPopupRetryButton.onClick.AddListener(Login);

                view.MoreOptionsButton.onClick.AddListener(view.ToggleOptionsPanelExpansion);

                // ThirdWeb
                view.EmailInputField.Submitted += OTPLogin;
            }
        }

        public override void Exit()
        {
            viewInstance.RestrictedUserContainer.SetActive(false);
            viewInstance!.ErrorPopupRoot.SetActive(false);

            view.MetamaskLoginButton.onClick.RemoveAllListeners();
            view.GoogleLoginButton.onClick.RemoveAllListeners();

            viewInstance.ErrorPopupCloseButton.onClick.RemoveAllListeners();
            viewInstance.ErrorPopupExitButton.onClick.RemoveAllListeners();

            // viewInstance.ErrorPopupRetryButton.onClick.RemoveAllListeners();

            view.MoreOptionsButton.onClick.RemoveAllListeners();

            // ThirdWeb
            view.EmailInputField.Submitted -= OTPLogin;
        }

        private void LoginWithMetamask() =>
            Login(LoginMethod.METAMASK);

        private void LoginWithGoogle() =>
            Login(LoginMethod.GOOGLE);

        private void Login(LoginMethod method)
        {
            compositeWeb3Provider.CurrentMethod = AuthMethod.DappWallet;

            view.ShowLoading();
            machine.Enter<IdentityVerificationDappAuthState, (LoginMethod, CancellationToken)>((method, controller.GetRestartedLoginToken()));
        }

        private void OTPLogin()
        {
            compositeWeb3Provider.CurrentMethod = AuthMethod.ThirdWebOTP;

            view.Hide();

            machine.Enter<IdentityVerificationOTPAuthState, (string, CancellationToken)>(
                payload: (viewInstance.LoginSelectionAuthView.EmailInputField.Text, controller.GetRestartedLoginToken()));
        }

        private void CancelLoginAndRestartFromBeginning()
        {
            controller.CancelLoginProcess();
            machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, -1), allowReEnterSameState: true);
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
