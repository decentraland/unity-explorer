using System;

namespace DCL.Web3.Authenticators
{
    public class Web3SignatureException : Exception
    {
        public Web3SignatureException(string message) : base(message) { }
    }
}
