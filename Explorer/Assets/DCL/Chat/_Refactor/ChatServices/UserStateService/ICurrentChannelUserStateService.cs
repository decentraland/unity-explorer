using System.Collections.Generic;

namespace DCL.Chat.ChatServices
{
    public interface ICurrentChannelUserStateService
    {
        IReadOnlyCollection<string> OnlineParticipants { get; }

        void Deactivate();
    }
}
