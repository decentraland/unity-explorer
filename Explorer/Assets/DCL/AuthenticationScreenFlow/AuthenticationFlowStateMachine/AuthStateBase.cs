using DCL.Diagnostics;
using MVC;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public abstract class AuthStateBase : IState
    {
        protected readonly AuthenticationScreenView viewInstance;

        protected AuthStateBase(AuthenticationScreenView viewInstance)
        {
            this.viewInstance = viewInstance;
        }

        public virtual void Enter()
        {
            viewInstance!.ErrorPopupRoot.SetActive(false);
            ReportHub.Log(ReportCategory.AUTHENTICATION, $"Enter state {GetType().Name}...");
        }

        public virtual void Exit()
        {
            ReportHub.Log(ReportCategory.AUTHENTICATION, $"Exit state {GetType().Name}...");
        }
    }
}
