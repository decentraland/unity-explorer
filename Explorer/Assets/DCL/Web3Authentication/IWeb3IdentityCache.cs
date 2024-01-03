using System;

namespace DCL.Web3Authentication
{
    public interface IWeb3IdentityCache : IDisposable
    {
        IWeb3Identity? Identity { get; set; }

        void Clear();
    }
}
