using System;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3EthereumApi
    {
        [Serializable]
        public struct LoginAuthApiRequest
        {
            public string method;

            // Newtonsoft-serialized wire DTO (auth-api SocketIO NewtonsoftJsonSerializer); Unity serialization never sees this field.
#pragma warning disable UAC1001
            public object[] @params;
#pragma warning restore UAC1001
        }
    }
}
