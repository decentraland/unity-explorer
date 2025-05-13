using System;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator
    {
        [Serializable]
        public struct LoginAuthApiRequest
        {
            public string method;
            public object[] @params;
        }
    }
}
