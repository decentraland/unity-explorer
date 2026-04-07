using System;

namespace DCL.Web3.Authenticators
{
    public class AutoLoginTokenInvalidException : Exception
    {
        public AutoLoginTokenInvalidException(string message) : base(message) { }
    }
}
