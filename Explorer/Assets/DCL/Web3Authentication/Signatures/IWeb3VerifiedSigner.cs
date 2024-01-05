using System;

namespace DCL.Web3Authentication.Signatures
{
    public interface IWeb3VerifiedSigner : IWeb3Signer
    {
        public delegate void VerificationDelegate(int code, DateTime expiration);

        void AddVerificationListener(VerificationDelegate callback);
    }
}
