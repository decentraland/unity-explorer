using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Utilities;
using DCL.Utility;
using DCL.Web3.Authenticators;
using MVC;
using System;
using System.Threading;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoginStartAuthState : AuthStateBase, IState, IPayloadedState<PopupType>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly LoginScreenSubView subView;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly SplashScreen splashScreen;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;

        public LoginStartAuthState(MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState, SplashScreen splashScreen,
            ICompositeWeb3Provider compositeWeb3Provider) : base(viewInstance)
        {
            subView = viewInstance.LoginScreenSubView;

            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.splashScreen = splashScreen;
            this.compositeWeb3Provider = compositeWeb3Provider;


            // Cancel button persists in the Verification state (until code is shown)
            subView.CancelLoginButton.onClick.AddListener(CancelLoginAndRestartFromBeginning);
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
                splashScreen.FadeOutAndHide();

            currentState.Value = AuthenticationStatus.Login;

            //-- GameObjects state setup
            subView.gameObject.SetActive(true);
            subView.SlideIn();

            // Listeners
            subView.MetamaskLoginButton.onClick.AddListener(LoginWithMetamask);
            subView.GoogleLoginButton.onClick.AddListener(LoginWithGoogle);

            viewInstance.ErrorPopupCloseButton.onClick.AddListener(CloseErrorPopup);
            viewInstance.ErrorPopupExitButton.onClick.AddListener(ExitUtils.Exit);

            // viewInstance.ErrorPopupRetryButton.onClick.AddListener(Login);

            subView.MoreOptionsButton.onClick.AddListener(subView.ToggleOptionsPanelExpansion);

            // ThirdWeb
            subView.EmailInputField.Submitted += OTPLogin;
        }

        public override void Exit()
        {
            viewInstance.RestrictedUserContainer.SetActive(false);
            viewInstance!.ErrorPopupRoot.SetActive(false);

            subView.MetamaskLoginButton.onClick.RemoveAllListeners();
            subView.GoogleLoginButton.onClick.RemoveAllListeners();

            viewInstance.ErrorPopupCloseButton.onClick.RemoveAllListeners();
            viewInstance.ErrorPopupExitButton.onClick.RemoveAllListeners();

            // viewInstance.ErrorPopupRetryButton.onClick.RemoveAllListeners();

            subView.MoreOptionsButton.onClick.RemoveAllListeners();

            // ThirdWeb
            subView.EmailInputField.Submitted -= OTPLogin;
        }

        private void LoginWithMetamask() =>
            Login(LoginMethod.METAMASK);

        private void LoginWithGoogle() =>
            Login(LoginMethod.GOOGLE);

        private void Login(LoginMethod method)
        {
            compositeWeb3Provider.CurrentMethod = AuthMethod.DappWallet;

            subView.ShowLoading();
            machine.Enter<IdentityAndVerificationAuthState, (LoginMethod, CancellationToken)>((method, controller.GetRestartedLoginToken()));
        }

        private void OTPLogin()
        {
            compositeWeb3Provider.CurrentMethod = AuthMethod.ThirdWebOTP;

            machine.Enter<IdentityAndOTPConfirmationState, (string, CancellationToken)>(
                payload: (viewInstance.LoginScreenSubView.EmailInputField.CurrentEmailText, controller.GetRestartedLoginToken()));
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
