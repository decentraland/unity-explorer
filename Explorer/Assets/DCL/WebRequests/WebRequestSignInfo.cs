using CommunicationData.URLHelpers;
using System;

namespace DCL.WebRequests
{
    /// <summary>
    ///     If this structure is present the WebRequest will be signed
    /// </summary>
    public readonly struct WebRequestSignInfo
    {
        private readonly string stringToSign;

        public string StringToSign => stringToSign;

        public WebRequestSignInfo(URLAddress signUrl) : this(signUrl.ToString()) { }

        public WebRequestSignInfo(string stringToSign)
        {
            this.stringToSign = stringToSign;
        }

        public static WebRequestSignInfo NewFromUrl(string url, ulong unixTimestamp, string method) =>
            NewFromRaw(string.Empty, url, unixTimestamp, method);

        /// <summary>
        ///     Builds the ADR-44 payload the auth chain is signed over.
        ///     <para>
        ///         The method and path are lowercased; the timestamp and metadata are interpolated verbatim.
        ///         That last part matters: folding the whole string, as this did before, left the metadata's
        ///         casing outside the signature while the request still delivered `x-identity-metadata`
        ///         unfolded. A key or value could therefore be re-cased between signing and delivery and still
        ///         verify, and services read that header — so they were authorizing on bytes the signature
        ///         never covered.
        ///     </para>
        ///     <para>
        ///         Matches createPayload in @dcl/crypto-middleware 6.x, so what is signed here is exactly what
        ///         every Decentraland verifier reconstructs.
        ///     </para>
        /// </summary>
        public static WebRequestSignInfo NewFromRaw(string rawToSign, string url, ulong unixTimestamp, string method)
        {
            string path = new Uri(url).AbsolutePath;
            string metadata = string.IsNullOrEmpty(rawToSign) ? "{}" : rawToSign;
            string payload = $"{method.ToLowerInvariant()}:{path.ToLowerInvariant()}:{unixTimestamp}:{metadata}";
            return new WebRequestSignInfo(payload);
        }

        public override string ToString() =>
            $"WebRequestSignInfo: Content to sign {stringToSign}";
    }
}
