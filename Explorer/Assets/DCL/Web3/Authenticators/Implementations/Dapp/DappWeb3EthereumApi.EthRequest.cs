using DCL.Web3.Chains;
using System;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3EthereumApi
    {
        [Serializable]
        private struct AuthorizedEthApiRequest
        {
            public string method;
            public object[] @params;
            public AuthLink[] authChain;
        }
    }
}
