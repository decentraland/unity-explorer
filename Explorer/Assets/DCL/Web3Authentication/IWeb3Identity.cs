using System;

namespace DCL.Web3Authentication
{
    public interface IWeb3Identity : IDisposable
    {
        string Address { get; }
        DateTime Expiration { get; }
        IWeb3Account EphemeralAccount { get; }

        AuthChain Sign(string entityId);
    }
}
