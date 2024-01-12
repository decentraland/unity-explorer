using System;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator
    {
        [Serializable]
        private struct LoginResponse
        {
            public string requestId;
            public string result;
            public string sender;
        }

        [Serializable]
        private struct MethodResponse<T>
        {
            public string requestId;
            public T result;
            public string sender;
        }
    }
}
