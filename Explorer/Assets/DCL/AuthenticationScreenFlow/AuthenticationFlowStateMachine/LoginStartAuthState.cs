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
        private readonly AuthLoginScreenView view;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly SplashScreen splashScreen;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;

        public LoginStartAuthState(MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState, SplashScreen splashScreen,
            ICompositeWeb3Provider compositeWeb3Provider) : base(viewInstance)
        {
            view = viewInstance.AuthLoginScreenView;

            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.splashScreen = splashScreen;
            this.compositeWeb3Provider = compositeWeb3Provider;


            // Cancel button persists in the Verification state (until code is shown)
            view.CancelLoginButton.onClick.AddListener(CancelLoginAndRestartFromBeginning);
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

        private void LoginWithMetamask() =>
            Login(LoginMethod.METAMASK);

        private void LoginWithGoogle() =>
            Login(LoginMethod.GOOGLE);

        public void Enter()
        {
            if (machine.PreviousState is InitAuthScreenState)
                splashScreen.Hide();

            currentState.Value = AuthenticationStatus.Login;

            //-- GameObjects state setup
            view.gameObject.SetActive(true);
            view.SlideIn();

            // Listeners
            view.MetamaskLoginButton.onClick.AddListener(LoginWithMetamask);
            view.GoogleLoginButton.onClick.AddListener(LoginWithGoogle);

            viewInstance.ErrorPopupCloseButton.onClick.AddListener(CloseErrorPopup);
            viewInstance.ErrorPopupExitButton.onClick.AddListener(ExitUtils.Exit);

            // viewInstance.ErrorPopupRetryButton.onClick.AddListener(Login);

            view.MoreOptionsButton.onClick.AddListener(view.ToggleOptionsPanelExpansion);

            // ThirdWeb
            view.EmailInput.StartButtonPressed += OTPLogin;
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
            view.EmailInput.StartButtonPressed -= OTPLogin;
        }

        private void Login(LoginMethod method)
        {
            compositeWeb3Provider.CurrentMethod = AuthMethod.DappWallet;

            view.ShowLoading();

            machine.Enter<IdentityAndVerificationAuthState, (LoginMethod, CancellationToken)>((method, controller.GetRestartedLoginToken()));
        }

        private void OTPLogin()
        {
            compositeWeb3Provider.CurrentMethod = AuthMethod.ThirdWebOTP;

            viewInstance.AuthLoginScreenView.SlideOut();

            machine.Enter<IdentityAndOTPConfirmationState, (string, CancellationToken)>(
                payload: (viewInstance.AuthLoginScreenView.EmailInput.CurrentEmailText, controller.GetRestartedLoginToken()));
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
