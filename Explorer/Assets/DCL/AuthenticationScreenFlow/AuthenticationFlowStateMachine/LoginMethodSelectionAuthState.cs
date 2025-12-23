using DCL.Utilities;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LoginMethodSelectionAuthState : AuthStateBase
    {
        private readonly ReactiveProperty<AuthenticationScreenController.AuthenticationStatus> currentState;

        public LoginMethodSelectionAuthState(AuthenticationScreenView? viewInstance,
            ReactiveProperty<AuthenticationScreenController.AuthenticationStatus> currentState) : base(viewInstance)
        {
            this.currentState = currentState;
        }

        public override void Enter()
        {
            base.Enter();
        }

        public override void Exit()
        {
            base.Exit();
        }
    }
}
