using System;

namespace DCL.Web3.Identities
{
    public class ProxyIdentityCache : IWeb3IdentityCache
    {
        private readonly MemoryWeb3IdentityCache memory;
        private readonly PlayerPrefsIdentityProvider storage;

        public ProxyIdentityCache(MemoryWeb3IdentityCache memory,
            PlayerPrefsIdentityProvider storage)
        {
            this.memory = memory;
            this.storage = storage;
        }

        public void Dispose()
        {
            memory.Dispose();
            storage.Dispose();
        }

        public event Action? OnIdentityCleared
        {
            add => memory.OnIdentityCleared += value;
            remove => memory.OnIdentityCleared -= value;
        }

        public event Action? OnIdentityChanged
        {
            add => memory.OnIdentityChanged += value;
            remove => memory.OnIdentityChanged -= value;
        }

        public IWeb3Identity? Identity
        {
            get
            {
                if (memory.Identity != null)
                    return memory.Identity;

                IWeb3Identity? storedIdentity = storage.Identity;

                if (storedIdentity != null)
                {
                    if (storedIdentity.IsExpired)
                        return null;

                    memory.Identity = storedIdentity;
                }

                return memory.Identity;
            }

            set
            {
                memory.Identity = value;
                storage.Identity = value;
            }
        }

        public void Clear()
        {
            memory.Clear();
            storage.Clear();
        }
    }
}
