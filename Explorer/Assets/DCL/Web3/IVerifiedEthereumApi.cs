using System;

namespace DCL.Web3
{
    public interface IVerifiedEthereumApi : IEthereumApi
    {
        public delegate void VerificationDelegate(int code, DateTime expiration);

        void AddVerificationListener(VerificationDelegate callback);
    }
}
