namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Builds the signature web app URL for the deep link sign-in flow.
    ///     The referrer is only appended when it is a strictly valid address,
    ///     since the value originates from an untrusted launch argument.
    /// </summary>
    public static class DeepLinkSignInUrl
    {
        public static string Build(string signatureWebAppUrl, string authRequestId, string loginMethod, bool bridgeOnly, string? referrer)
        {
            string url = $"{signatureWebAppUrl}/{authRequestId}?loginMethod={loginMethod}&flow=deeplink";

            if (bridgeOnly)
                url += "&bridgeOnly";

            string? normalizedReferrer = ReferrerArg.Normalize(referrer);

            if (normalizedReferrer != null)
                url += $"&referrer={normalizedReferrer}";

            return url;
        }
    }
}
