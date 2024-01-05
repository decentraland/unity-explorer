using System;

namespace DCL.Web3Authentication.Signatures
{
    public class SignatureExpiredException : Exception
    {
        public SignatureExpiredException(DateTime expiration)
            : base($"Signature expired: {expiration}") { }
    }
}
