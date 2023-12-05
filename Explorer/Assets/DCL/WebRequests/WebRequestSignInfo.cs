using CommunicationData.URLHelpers;

namespace DCL.WebRequests
{
    /// <summary>
    ///     If this structure is present the WebRequest will be signed
    /// </summary>
    public readonly struct WebRequestSignInfo
    {
        public readonly URLAddress SignUrl;

        public WebRequestSignInfo(URLAddress signUrl)
        {
            SignUrl = signUrl;
        }
    }
}
