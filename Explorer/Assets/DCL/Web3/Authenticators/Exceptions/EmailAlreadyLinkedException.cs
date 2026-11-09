using System;

namespace DCL.Web3.Authenticators
{
    public class EmailAlreadyLinkedException : Exception
    {
        public EmailAlreadyLinkedException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}