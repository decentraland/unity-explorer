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
        None = 0,

        /// <summary>
        ///     From the resources bundled at build time
        /// </summary>
        Embedded = 1,

        /// <summary>
        ///     Downloaded over network
        /// </summary>
        Web = 1 << 1,

        /// <summary>
        ///     Downloaded over Addressables
        /// </summary>
        Addressable = 1 << 2,

        /// <summary>
        ///     All sources
        /// </summary>
        All = Embedded | Web | Addressable,
    }

    public static class AssetSourceEnumExtensions
    {
        private static readonly Dictionary<AssetSource, string> CURRENT_SOURCE_STRINGS = new ()
        {
            {
                AssetSource.Addressable, "ADDRESSABLE"
            },
            {
                AssetSource.Embedded, "EMBEDDED"
            },
            {
                AssetSource.Web, "WEB"
            },
            {
                AssetSource.None, "NONE"
            },
        };

        public static string ToStringNonAlloc(this AssetSource source) =>
            CURRENT_SOURCE_STRINGS[source]!;
    }
}
