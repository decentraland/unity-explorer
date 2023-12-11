using System;

namespace DCL.Web3Authentication
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
