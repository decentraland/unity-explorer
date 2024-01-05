using DCL.Web3Authentication.Chains;
using System;

namespace DCL.Web3Authentication.Signatures
{
    public partial class DappWeb3Authenticator
    {
        [Serializable]
        private struct SignatureRequest
        {
            public string method;
            public string[] @params;
        }

        [Serializable]
        private struct AuthorizedSignatureRequest
        {
            public string method;
            public string[] @params;
            public AuthLink[] authChain;
        }
    }
}
