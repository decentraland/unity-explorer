namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class InitAuthScreenState : AuthStateBase
    {
        public InitAuthScreenState(AuthenticationScreenView viewInstance) : base(viewInstance) { }

        public override void Enter()
        {
            base.Enter();
            viewInstance.LoginContainer.SetActive(false);
            viewInstance.VerificationContainer.SetActive(false);
            viewInstance.FinalizeContainer.SetActive(false);

            viewInstance.RestrictedUserContainer.SetActive(false);
            viewInstance.ErrorPopupRoot.SetActive(false);
        }

        public override void Exit()
        {
            base.Exit();
        }
    }
}
