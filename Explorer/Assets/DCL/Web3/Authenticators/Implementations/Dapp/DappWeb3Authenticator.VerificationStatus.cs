using System;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator
    {
        [Serializable]
        private struct CodeVerificationStatus
        {
            public string requestId;
        }
    }
}
