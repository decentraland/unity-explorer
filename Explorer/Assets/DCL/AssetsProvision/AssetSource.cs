using System;
using System.Collections.Generic;

namespace AssetManagement
{
    /// <summary>
    ///     Possible sources an asset can come from.
    ///     Should be sorted by priority in an ascending order
    /// </summary>
    [Flags]
    public enum AssetSource
    {
        NONE = 0,

        /// <summary>
        ///     From the resources bundled at build time
        /// </summary>
        EMBEDDED = 1,

        /// <summary>
        ///     Load from memory or disk cache
        /// </summary>
        CACHE = 1 << 1,

        /// <summary>
        ///     Downloaded over network
        /// </summary>
        WEB = 2 << 2,

        /// <summary>
        ///     Downloaded over Addressables
        /// </summary>
        ADDRESSABLE = 1 << 4,

        /// <summary>
        ///     All sources
        /// </summary>
        ALL = EMBEDDED | CACHE | WEB | ADDRESSABLE,
    }

    public static class AssetSourceEnumExtensions
    {
        private static readonly Dictionary<AssetSource, string> CURRENT_SOURCE_STRINGS = new ()
        {
            {
                AssetSource.ADDRESSABLE, "ADDRESSABLE"
            },
            {
                AssetSource.EMBEDDED, "EMBEDDED"
            },
            {
                AssetSource.WEB, "WEB"
            },
            {
                AssetSource.CACHE, "CACHE"
            },
            {
                AssetSource.NONE, "NONE"
            },
        };

        public static string ToStringNonAlloc(this AssetSource source) =>
            CURRENT_SOURCE_STRINGS[source]!;
    }
}
