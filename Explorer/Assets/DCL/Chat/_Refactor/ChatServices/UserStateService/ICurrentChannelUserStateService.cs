using System.Collections.Generic;

namespace DCL.Chat.ChatServices
{
    public interface ICurrentChannelUserStateService
    {
        IReadOnlyCollection<string> OnlineParticipants { get; }

        /// <summary>
        ///     Copies the current online participants into the provided destination set, safe from concurrent modification.
        /// </summary>
        void CopyOnlineParticipantsTo(HashSet<string> destination);

        void Deactivate();
    }
}
