using System;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator
    {
        [Serializable]
        private struct LoginAuthApiResponse
        {
            public string requestId;
            public string result;
            public string sender;
        }
    }
}
