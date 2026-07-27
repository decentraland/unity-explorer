namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Builds the signature web app URL for the deep link sign-in flow.
    ///     <para>
    ///         Why the referrer travels on this URL at all: wallet users create their profile in
    ///         the auth website's setup flow, and that web flow is what registers the referral for
    ///         them — the in-client registration in <c>LobbyForNewAccountAuthState</c> only runs
    ///         for accounts created in-client (email/OTP), which never open the browser. The two
    ///         paths are disjoint, and the backend treats a duplicate registration as an
    ///         idempotent no-op, so any overlap is safe.
    ///     </para>
    /// </summary>
    public static class DeepLinkSignInUrl
    {
        public static string Build(string signatureWebAppUrl, string authRequestId, string loginMethod, bool bridgeOnly, Web3Address? referrer)
        {
            string url = $"{signatureWebAppUrl}/{authRequestId}?loginMethod={loginMethod}&flow=deeplink";

            if (bridgeOnly)
                url += "&bridgeOnly";

            // Defense-in-depth: Web3Address lowercases but does not validate on construction,
            // so re-check here — the last point before the value reaches the URL.
            if (referrer is { } address && Web3Address.IsValid(address))
                url += $"&referrer={address}";

            return url;
        }
    }
}
