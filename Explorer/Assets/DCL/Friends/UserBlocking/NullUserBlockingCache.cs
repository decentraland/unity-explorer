using System;

namespace DCL.Friends.UserBlocking
{
    /// <summary>
    ///     No-op implementation used when the user-blocking feature is disabled.
    ///     Events are never raised; <see cref="UserIsBlocked"/> always returns false.
    /// </summary>
    public class NullUserBlockingCache : IUserBlockingCache
    {
        public event Action<string>? UserBlocked { add { } remove { } }
        public event Action<string>? UserBlocksYou { add { } remove { } }
        public event Action<string>? UserUnblocked { add { } remove { } }
        public event Action<string>? UserUnblocksYou { add { } remove { } }

        public bool HideChatMessages { get; set; }

        public bool UserIsBlocked(string userId) => false;
    }
}
