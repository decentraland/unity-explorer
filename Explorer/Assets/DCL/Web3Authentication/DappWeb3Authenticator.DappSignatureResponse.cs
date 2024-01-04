using System;

namespace DCL.Web3Authentication
{
    public partial class DappWeb3Authenticator
    {
        [Serializable]
        private struct DappSignatureResponse
        {
            public string requestId;
            public string result;
            public string sender;
        }
    }
}
