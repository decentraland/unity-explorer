
namespace DCL.Friends.UserBlocking
{
    public interface IUserBlockingCache
    {
        ReadOnlyHashSet<string> BlockedUsers { get; }
        ReadOnlyHashSet<string> BlockedByUsers { get; }

        bool HideChatMessages { get; set; }

        bool UserIsBlocked(string userId);
    }
}
