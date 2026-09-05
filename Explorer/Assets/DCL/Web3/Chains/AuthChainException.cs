using System;

namespace DCL.Web3.Chains
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
