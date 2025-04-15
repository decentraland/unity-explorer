using DCL.UI;

namespace DCL.Chat
{
    public interface IChatController
    {
        public string IslandRoomSid { get; }
        public string PreviousRoomSid { get; set; }
        public bool TryGetView(out ChatView view);
        void SetInputWithUserState(ChatUserStateUpdater.ChatUserState state);
        void UpdateConversationToolbarStatusIcon(string userId, OnlineStatus status);
    }
}
