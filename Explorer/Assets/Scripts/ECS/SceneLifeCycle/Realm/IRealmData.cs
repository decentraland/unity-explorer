using DCL.Ipfs;
using Ipfs;

namespace ECS
{
    /// <summary>
    ///     Readonly interface to fetch realm data
    /// </summary>
    public interface IRealmData
    {
        IIpfsRealm Ipfs { get; }

        /// <summary>
        ///     Indicates that the realm contains a fixed number of scenes
        /// </summary>
        bool ScenesAreFixed { get; }

        /// <summary>
        ///     Whether the data was set at least once
        /// </summary>
        bool Configured { get; }
    }
}
