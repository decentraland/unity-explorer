using System;

namespace DCL.Web3Authentication
{
    public interface IWeb3Identity : IDisposable
    {
        Web3Address Address { get; }
        DateTime Expiration { get; }
        IWeb3Account EphemeralAccount { get; }

        AuthChain Sign(string entityId);
    }
}
