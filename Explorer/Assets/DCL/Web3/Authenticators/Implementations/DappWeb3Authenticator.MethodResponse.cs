using System;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator
    {
        [Serializable]
        private struct MethodResponse
        {
            public string requestId;
            public object result;
            public string sender;
        }
    }
}
