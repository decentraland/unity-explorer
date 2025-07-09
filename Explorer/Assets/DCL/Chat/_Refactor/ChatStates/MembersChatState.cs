using DCL.Diagnostics;

namespace DCL.Chat.ChatStates
{
    /// <summary>
    /// Purpose: Displaying the list of members in the current channel.
    /// begin() (Entry Actions):
    ///     UI: Hide the message feed view, show the member list view. view.SetMemberListViewVisibility(true);
    ///     Presenter: Activate the member list presenter to fetch and display data. _context.memberListPresenter.Activate();
    ///     Subscribe: _context.titleBarPresenter.OnMemberListToggle += OnToggleMembers; (Listens for the "close members" button)
    /// end() (Exit Actions):
    ///     UI: Show the message feed, hide the member list. view.SetMemberListViewVisibility(false);
    ///     Presenter: Deactivate the member list presenter. _context.memberListPresenter.Deactivate();
    ///     Unsubscribe: from OnMemberListToggle.
    /// Event Handlers:
    ///     OnToggleMembers(false) -> _machine.changeState<DefaultChatState>();
    /// </summary>
    public class MembersChatState : ChatState
    {
        public override void begin()
        {
            ReportHub.Log(ReportCategory.UNSPECIFIED, "[ChatState] MembersChatState: begin");
        }

        public override void end()
        {
            ReportHub.Log(ReportCategory.UNSPECIFIED, "[ChatState] MembersChatState: begin");
        }
    }
}