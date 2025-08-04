using DCL.Friends.UserBlocking;

namespace DCL.Chat.ChatServices
{
    public interface ICurrentChannelUserStateService
    {
        ReadOnlyHashSet<string> OnlineParticipants { get; }

        void Deactivate();
    }
}
