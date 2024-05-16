using System;

namespace DCL.Web3.Identities
{
    public class Web3IdentityMissingException : Exception
    {
        public Web3IdentityMissingException(string message) : base(message) { }
    }
}
