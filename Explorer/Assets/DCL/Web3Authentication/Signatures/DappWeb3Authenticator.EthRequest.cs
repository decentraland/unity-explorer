using DCL.Web3Authentication.Chains;
using System;

namespace DCL.Web3Authentication.Signatures
{
    public partial class DappWeb3Authenticator
    {
        [Serializable]
        private class EthApiRequest
        {
            public string method;
            public string[] @params;
        }

        [Serializable]
        private class AuthorizedEthApiRequest : EthApiRequest
        {
            public AuthLink[] authChain;
        }
    }
}
