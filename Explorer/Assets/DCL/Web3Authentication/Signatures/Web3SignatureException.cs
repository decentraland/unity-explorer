using System;

namespace DCL.Web3Authentication.Signatures
{
    public class Web3SignatureException : Exception
    {
        public Web3SignatureException(string message)
            : base(message) { }
    }
}
