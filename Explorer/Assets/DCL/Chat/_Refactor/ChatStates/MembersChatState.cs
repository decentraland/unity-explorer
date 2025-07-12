namespace DCL.Chat.ChatStates
{
    public class MembersChatState : ChatState
    {
        public override void begin()
        {
            _context.messageViewerPresenter?.Hide();
            _context.chatInputPresenter?.Hide();
            _context.titleBarPresenter.SwitchToMembersMode();
            _context.memberListPresenter?.Activate();
            _context.memberListPresenter?.ShowAndLoadCurrentList();
            _context.titleBarPresenter.OnMemberListToggle += OnMemberListToggled;
        }

        public override void end()
        {
            _context.titleBarPresenter.OnMemberListToggle += OnMemberListToggled;
        }

        private void OnMemberListToggled(bool active)
        {
            if (!active)
            {
                _machine.changeState<FocusedChatState>();
            }
        }
    }
}