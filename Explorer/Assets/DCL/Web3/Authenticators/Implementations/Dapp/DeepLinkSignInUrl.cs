namespace DCL.Web3.Authenticators
{
    public static class DeepLinkSignInUrl
    {
        public static string Build(string signatureWebAppUrl, string authRequestId, string loginMethod, Web3Address? referrer)
        {
            string url = $"{signatureWebAppUrl}/{authRequestId}?loginMethod={loginMethod}&flow=deeplink&bridgeOnly";

            // Defense-in-depth: Web3Address lowercases but does not validate on construction,
            // so re-check here — the last point before the value reaches the URL.
            if (referrer is { } address && Web3Address.IsValidWalletAddress(address))
                url += $"&referrer={address}";

            return url;
        }
    }
}
