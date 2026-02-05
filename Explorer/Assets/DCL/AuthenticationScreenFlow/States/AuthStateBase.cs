using MVC;

namespace DCL.AuthenticationScreenFlow.States
{
    public abstract class AuthStateBase : IExitableState
    {
        protected readonly AuthenticationScreenView viewInstance;

        protected AuthStateBase(AuthenticationScreenView viewInstance)
        {
            this.viewInstance = viewInstance;
        }

        public virtual void Exit() {}
    }
}
