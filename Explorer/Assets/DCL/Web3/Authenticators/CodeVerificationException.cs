using System;

namespace DCL.Web3.Authenticators
{
    public class CodeVerificationException : Exception
    {
        public CodeVerificationException(string message)
            : base(message) { }
    }
}
