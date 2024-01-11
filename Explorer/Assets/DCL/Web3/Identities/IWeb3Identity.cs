using DCL.Web3.Accounts;
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
    }
}
