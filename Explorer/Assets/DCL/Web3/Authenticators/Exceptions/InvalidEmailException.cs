using System;

namespace DCL.Web3.Authenticators
{
    public class InvalidEmailException : Exception
    {
        public InvalidEmailException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
