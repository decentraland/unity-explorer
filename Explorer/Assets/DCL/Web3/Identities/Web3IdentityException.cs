using System;

namespace DCL.Web3.Identities
{
    public class Web3IdentityException : Exception
    {
        public IWeb3Identity Identity { get; }

        public Web3IdentityException(IWeb3Identity identity, string message)
            : base(message)
        {
            Identity = identity;
        }
    }
}
