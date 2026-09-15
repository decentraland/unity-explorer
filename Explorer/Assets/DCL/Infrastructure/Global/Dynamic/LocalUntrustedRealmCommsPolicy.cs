using System;
using Utility.Networking;

namespace Global.Dynamic
{
    /// <summary>
    ///     Selects the local ICE workaround without changing the transport policy for remote realms.
    /// </summary>
    public static class LocalUntrustedRealmCommsPolicy
    {
        /// <summary>
        ///     Direct ICE is opt-in and limited to a loopback realm. The LiveKit SDK applies the final
        ///     URL-level loopback check as well, so entering a remote world later cannot inherit it.
        /// </summary>
        public static bool ShouldUseTransportAll(bool acceptUntrustedRealm, string realmUrl) =>
            acceptUntrustedRealm && LoopbackUrls.IsLoopbackWebUrl(realmUrl.AsSpan());
    }
}
