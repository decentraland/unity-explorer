using DCL.Diagnostics;
using MVC;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public readonly struct AuthStateContext { }

    public abstract class AuthStateBase : MVCState<AuthStateBase, AuthStateContext>
    {
        protected readonly AuthenticationScreenView viewInstance;

        protected AuthStateBase(AuthenticationScreenView viewInstance)
        {
            this.viewInstance = viewInstance;
        }

        public override void Enter()
        {
            ReportHub.Log(ReportCategory.AUTHENTICATION, $"Enter state {GetType().Name}");
        }

        public override void Exit()
        {
            ReportHub.Log(ReportCategory.AUTHENTICATION, $"Exit state {GetType().Name}");
        }
    }
}
