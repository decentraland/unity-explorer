using System;

namespace DCL.Web3.Authenticators
{
    public class SignatureExpiredException : Exception
    {
        public SignatureExpiredException(DateTime expiration)
            : base($"Signature expired: {expiration}") { }
    }
}
