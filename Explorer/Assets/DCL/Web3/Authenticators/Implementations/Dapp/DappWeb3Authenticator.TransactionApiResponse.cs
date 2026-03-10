using System;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator
    {
        [Serializable]
        public struct TransactionApiResponse
        {
            public string txHash;
        }
    }
}