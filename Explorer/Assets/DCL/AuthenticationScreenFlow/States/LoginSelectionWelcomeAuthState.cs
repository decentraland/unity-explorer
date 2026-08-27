using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Utilities;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using static DCL.UI.UIAnimationHashes;

namespace DCL.AuthenticationScreenFlow
{
    public class LoginSelectionWelcomeAuthState : AuthStateBase, IState
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;
        private readonly SplashScreen splashScreen;
        private readonly LoginSelectionWelcomeAuthView view;

        private Exception? loginException;

        public LoginSelectionWelcomeAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            ICompositeWeb3Provider compositeWeb3Provider,
            SplashScreen splashScreen) : base(viewInstance)
        {
            view = viewInstance.LoginSelectionWelcomeAuthView;

            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.compositeWeb3Provider = compositeWeb3Provider;
            this.splashScreen = splashScreen;
        }

        public new void Enter()
        {
            base.Enter();

            if (splashScreen != null)
                splashScreen.FadeOutAndHide();

            loginException = null;

            currentState.Value = AuthStatus.LoginSelectionScreen;

            view.Show();
            SetButtonsInteractable(true);

            view.LoginOrSignupButton.onClick.AddListener(OnLoginClicked);
            view.PlayAsGuestButton.onClick.AddListener(PlayAsGuestClicked);
        }

        public override void Exit()
        {
            if (loginException != null)
            {
                spanErrorInfo = loginException switch
                                {
                                    OperationCanceledException => new SpanErrorInfo("Guest login was cancelled"),
                                    { } ex => new SpanErrorInfo("Unexpected error during guest login", ex),
                                };

                if (loginException is not OperationCanceledException)
                    ReportHub.LogException(loginException, new ReportData(ReportCategory.AUTHENTICATION));
            }

            view.LoginOrSignupButton.onClick.RemoveAllListeners();
            view.PlayAsGuestButton.onClick.RemoveAllListeners();

            view.Hide();
            base.Exit();
        }

        private void OnLoginClicked() =>
            machine.Enter<LoginSelectionAuthState, int>(SLIDE);

        private void PlayAsGuestClicked()
        {
            SetButtonsInteractable(false);
            LoginAsGuestAsync(controller.GetRestartedLoginToken()).Forget();
        }

        private async UniTaskVoid LoginAsGuestAsync(CancellationToken ct)
        {
            compositeWeb3Provider.CurrentProvider = AuthProvider.Guest;
            controller.CurrentLoginMethod = LoginMethod.GUEST;
            currentState.Value = AuthStatus.LoginRequested;

            try
            {
                IWeb3Identity identity = await compositeWeb3Provider.LoginAsync(LoginPayload.ForGuestFlow(), ct);
                machine.Enter<ProfileFetchingAuthState, ProfileFetchingPayload>(new ProfileFetchingPayload(identity, false, ct));
            }
            catch (OperationCanceledException e)
            {
                loginException = e;
                SetButtonsInteractable(true);
            }
            catch (Exception e)
            {
                loginException = e;
                machine.Enter<LoginSelectionAuthState, ErrorType>(ErrorType.ConnectionError);
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            view.LoginOrSignupButton.interactable = interactable;
            view.PlayAsGuestButton.interactable = interactable;
        }
    }
}
