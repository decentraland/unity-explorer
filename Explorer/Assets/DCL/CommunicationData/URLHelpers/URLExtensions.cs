using System;

namespace CommunicationData.URLHelpers
{
    /// <summary>
    ///     Contains shortcuts to manipulate with URLs
    /// </summary>
    public static class URLExtensions
    {
        public static URLAddress Append(in this URLDomain address, in URLPath path) =>
            URLBuilder.Combine(address, path);

        public static URLDomain Append(in this URLDomain address, in URLSubdirectory subdirectory) =>
            URLBuilder.Combine(address, subdirectory);

    }
}
