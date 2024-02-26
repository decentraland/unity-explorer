using System;

namespace DCL.Web3.Identities
{
    public interface IWeb3IdentityCache : IDisposable
    {
        IWeb3Identity? Identity { get; set; }

        void Clear();
    }
}
