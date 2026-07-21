using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Global.AppArgs
{
    /// <summary>
    /// Deny-by-default allowlist of query params a <c>decentraland://</c> deep link may inject into app-args.
    /// Shared by the cold-start argv path and the runtime bridge path (both funnel through
    /// <see cref="ApplicationParametersParser.ProcessDeepLinkParameters"/>). The permitted set is a product
    /// decision (SEC-019/020 "Design affected") — changing it requires product sign-off.
    /// </summary>
    public static class DeepLinkAllowlist
    {
        public static readonly IReadOnlyCollection<string> PERMITTED_KEYS = new HashSet<string>
        {
            AppArgsFlags.REALM,
            AppArgsFlags.POSITION,
            AppArgsFlags.COMMUNITY,
            AppArgsFlags.SIGNIN,
            AppArgsFlags.AUTH_REQUEST_ID,
            AppArgsFlags.FORCE_OPEN_BACKPACK,
        };

        public static bool IsPermitted(string key) =>
            ((HashSet<string>)PERMITTED_KEYS).Contains(key);
    }
}
