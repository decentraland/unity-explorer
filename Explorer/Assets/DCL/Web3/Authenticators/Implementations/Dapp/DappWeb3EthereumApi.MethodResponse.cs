using System;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3EthereumApi
    {
        [Serializable]
        private struct MethodResponse
        {
            public string requestId;

            // Newtonsoft-deserialized wire DTO (auth-api SocketIO NewtonsoftJsonSerializer); Unity serialization never sees this field.
#pragma warning disable UAC1001
            public object result;
#pragma warning restore UAC1001

            public string sender;
        }
    }
}
