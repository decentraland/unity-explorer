using System;

namespace DCL.Web3Authentication
{
    public partial class DappWeb3Authenticator
    {
        [Serializable]
        private struct SignatureRequest
        {
            public string method;
            public string[] @params;
        }
    }
}
