using System;

namespace DCL.Web3.Authenticators
{
    public class CodeVerificationException : Exception
    {
        public CodeVerificationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
