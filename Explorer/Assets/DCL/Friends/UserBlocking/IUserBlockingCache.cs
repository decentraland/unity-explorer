
using DCL.Utility;
using System;

namespace DCL.Friends.UserBlocking
{
    public interface IUserBlockingCache
    {
        ReadOnlyHashSet<string> BlockedUsers { get; }
        ReadOnlyHashSet<string> BlockedByUsers { get; }

        // <summary>
        //     Event triggered when you block another user
        // </summary>
        event Action<string>? UserBlocked;

        // <summary>
        //     Event triggered when another user blocks you
        // </summary>
        event Action<string>? UserBlocksYou;

        // <summary>
        //     Event triggered when you unblock another user.
        // </summary>
        event Action<string>? UserUnblocked;

        // <summary>
        //     Event triggered when another user unblocks you
        // </summary>
        event Action<string>? UserUnblocksYou;

        bool HideChatMessages { get; set; }

        bool UserIsBlocked(string userId);
    }
}
