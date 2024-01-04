using System;

namespace DCL.Web3Authentication.Authenticators
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
