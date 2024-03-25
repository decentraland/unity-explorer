using CommunicationData.URLHelpers;

namespace DCL.WebRequests
{
    /// <summary>
    ///     If this structure is present the WebRequest will be signed
    /// </summary>
    public readonly struct WebRequestSignInfo
    {
        public readonly string StringToSign;

        public WebRequestSignInfo(URLAddress signUrl) : this(signUrl.ToString())
        {
        }

        public WebRequestSignInfo(string stringToSign)
        {
            this.StringToSign = stringToSign;
        }

        public override string ToString() =>
            $"WebRequestSignInfo: Content to sign {StringToSign}";
    }
}
