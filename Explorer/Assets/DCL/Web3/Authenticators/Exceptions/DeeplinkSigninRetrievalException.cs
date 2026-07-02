namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Raised when fetching the identity minted by the deep-link sign-in flow
    ///     (<c>GET /identities/{identityId}</c>) fails with a known status code.
    /// </summary>
    public class DeeplinkSigninRetrievalException : Web3Exception
    {
        public enum ErrorReason
        {
            /// <summary>404: the identity does not exist or has already been retrieved (identities are single-use).</summary>
            NOT_FOUND,

            /// <summary>410: the identity expired before retrieval (server-side TTL is 15 minutes).</summary>
            EXPIRED,

            /// <summary>403: the retrieval came from a different IP than the one that stored the identity (commonly a VPN or private relay).</summary>
            IP_MISMATCH,
        }

        public ErrorReason Reason { get; }

        public DeeplinkSigninRetrievalException(ErrorReason reason, string identityId)
            : base(MessageFor(reason, identityId))
        {
            Reason = reason;
        }

        private static string MessageFor(ErrorReason reason, string identityId) =>
            reason switch
            {
                ErrorReason.NOT_FOUND => $"Signin identity {identityId} was not found or was already retrieved",
                ErrorReason.EXPIRED => $"Signin identity {identityId} expired before it was retrieved",
                ErrorReason.IP_MISMATCH => $"Signin identity {identityId} was stored from a different IP: a VPN or private relay is likely interfering",
                _ => $"Signin identity {identityId} retrieval failed: {reason}",
            };
    }
}
