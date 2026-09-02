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

            // Newtonsoft-serialized wire DTO (auth-api SocketIO NewtonsoftJsonSerializer); Unity serialization never sees this field.
#pragma warning disable UAC1001
            public object[] @params;
#pragma warning restore UAC1001

            // Unix time in milliseconds covered by the signed auth chain together with method and params.
            public long timestamp;

            public AuthLink[] authChain;
        }
    }
}
