using DCL.Web3.Abstract;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Chains;
using System;

namespace DCL.Web3.Identities
{
    public interface IWeb3Identity : IDisposable
    {
        Web3Address Address { get; }
        DateTime Expiration { get; }
        IWeb3Account EphemeralAccount { get; }
        bool IsExpired { get; }
        AuthChain AuthChain { get; }

        AuthChain Sign(string entityId);

        class Random : IWeb3Identity
        {
            private static readonly IWeb3AccountFactory ACCOUNT_FACTORY = new Web3AccountFactory();

            public Random(IWeb3Account? account = null) : this(
                ACCOUNT_FACTORY.CreateRandomAccount().Address,
                DateTime.MaxValue,
                account ?? ACCOUNT_FACTORY.CreateRandomAccount(),
                false,
                AuthChain.Create()
            ) { }

            private Random(Web3Address address, DateTime expiration, IWeb3Account ephemeralAccount, bool isExpired, AuthChain authChain)
            {
                Address = address;
                Expiration = expiration;
                EphemeralAccount = ephemeralAccount;
                IsExpired = isExpired;
                AuthChain = authChain;
            }

            public Web3Address Address { get; }
            public DateTime Expiration { get; }
            public IWeb3Account EphemeralAccount { get; }
            public bool IsExpired { get; }
            public AuthChain AuthChain { get; }

            public AuthChain Sign(string entityId) =>
                throw new Exception("RandomIdentity cannot sign anything");

            public void Dispose() =>
                AuthChain.Dispose();
        }
    }
}
