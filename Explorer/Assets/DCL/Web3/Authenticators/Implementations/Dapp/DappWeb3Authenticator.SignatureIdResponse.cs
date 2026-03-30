using System;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator
    {
        [Serializable]
        private struct SignatureIdResponse
        {
            public string requestId;
            public string? expiration;
            public int code;
            public string? error;
        }
    }
}
