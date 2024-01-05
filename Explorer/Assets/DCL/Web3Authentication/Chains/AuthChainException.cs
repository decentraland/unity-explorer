using System;

namespace DCL.Web3Authentication.Chains
{
    public class AuthChainException : Exception
    {
        public AuthChain AuthChain { get; }

        public AuthChainException(AuthChain authChain, string message)
            : base(message)
        {
            AuthChain = authChain;
        }
    }
}
