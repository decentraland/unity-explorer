
namespace DCL.Friends
{
    public interface IUserBlockingCache
    {
        ReadOnlyHashSet<string> BlockedUsers { get; }
        ReadOnlyHashSet<string> BlockedByUsers { get; }
    }
}
