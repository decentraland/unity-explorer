using DCL.Web3Authentication.Accounts;
using DCL.Web3Authentication.Chains;
using System;

namespace DCL.Web3Authentication.Identities
{
    public interface IWeb3Identity : IDisposable
    {
        Web3Address Address { get; }
        DateTime Expiration { get; }
        IWeb3Account EphemeralAccount { get; }
        bool IsExpired { get; }

        AuthChain Sign(string entityId);
    }
}
