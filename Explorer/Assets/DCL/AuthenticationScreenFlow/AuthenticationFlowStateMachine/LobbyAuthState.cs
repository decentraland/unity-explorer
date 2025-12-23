namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LobbyAuthState : AuthStateBase
    {
        private readonly AuthenticationScreenCharacterPreviewController? characterPreviewController;

        public LobbyAuthState(AuthenticationScreenView? viewInstance,
            AuthenticationScreenCharacterPreviewController? characterPreviewController) : base(viewInstance)
        {
            this.characterPreviewController = characterPreviewController;
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
