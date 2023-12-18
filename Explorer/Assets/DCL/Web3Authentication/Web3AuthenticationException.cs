using System;

namespace DCL.Web3Authentication
{
    public class Web3AuthenticationException : Exception
    {
        public Web3AuthenticationException(string message)
            : base(message) { }
    }
}
