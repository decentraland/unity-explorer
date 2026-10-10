using System;

namespace DCL.Web3.Identities
{
    public class MemoryWeb3IdentityCache : IWeb3IdentityCache
    {
        private IWeb3Identity? identity;

        public event Action? OnIdentityCleared;
        public event Action? OnIdentityChanged;

        public IWeb3Identity? Identity
        {
            get => identity;

            set
            {
                if (identity == value) return;

                identity = value;

                if (identity != null)
                    OnIdentityChanged?.Invoke();
            }
        }

        public void Dispose()
        {
            Identity?.Dispose();
        }

        public void Clear()
        {
            if (Identity == null) return;
            Identity = null;
            OnIdentityCleared?.Invoke();
        }
    }
}
