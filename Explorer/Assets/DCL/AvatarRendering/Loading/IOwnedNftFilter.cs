using CommunicationData.URLHelpers;

namespace DCL.AvatarRendering.Loading
{
    /// <summary>
    ///     Suppresses NFTs that should no longer count as owned (e.g. a transfer initiated locally but not yet
    ///     indexed). Implementations must be thread-safe.
    /// </summary>
    public interface IOwnedNftFilter
    {
        bool ShouldExclude(URN fullUrn);
    }
}
