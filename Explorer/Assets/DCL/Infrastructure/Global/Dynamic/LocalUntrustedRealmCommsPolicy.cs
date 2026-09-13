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

        /// <summary>
        ///     Hands the computed policy to the comms SDK. The pinned livekit-sdk revision
        ///     (222d67cc, kept for its Linux support) predates the SDK-side
        ///     FFIBridgeExtensions.UseTransportAllForLoopbackUrls switch, so the policy is
        ///     accepted and dropped here until the pin advances to a revision that carries
        ///     both Linux support and the switch.
        /// </summary>
        public static void ApplyTransportPolicy(bool useTransportAllForLoopbackUrls)
        {
            _ = useTransportAllForLoopbackUrls;
        }
    }
}
