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

        public static WebRequestSignInfo? NewFromRaw(string? rawToSign, string url, ulong unixTimestamp, string method)
        {
            if (rawToSign == null)
                return null;

            string path = new Uri(url).AbsolutePath;
            string payload = $"{method}:{path}:{unixTimestamp}:{rawToSign}".ToLower();
            return new WebRequestSignInfo(payload);
        }

        public override string ToString() =>
            $"WebRequestSignInfo: Content to sign {stringToSign}";
    }
}
