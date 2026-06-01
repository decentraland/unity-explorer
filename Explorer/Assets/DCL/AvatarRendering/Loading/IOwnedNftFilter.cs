using CommunicationData.URLHelpers;

namespace DCL.AvatarRendering.Loading
{
    /// <summary>
    ///     Predicate used by owner-scoped lambda loaders to suppress NFTs that should not be considered
    ///     owned anymore (e.g. tokens whose transfer has been initiated locally but not yet indexed).
    ///     Implementations must be thread-safe.
    /// </summary>
    public interface IOwnedNftFilter
    {
        bool ShouldExclude(URN fullUrn);
    }
}
