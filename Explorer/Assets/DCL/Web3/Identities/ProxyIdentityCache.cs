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
                    // Since an expired identity is like non having one at all
                    // we return an invalid one to force the systems to not use it
                    // as not all of them are validating its expiration
                    if (storedIdentity.IsExpired)
                    {
                        // We also need to clear it, otherwise it produces inconsistencies
                        // After re-login it was using an old identity
                        storage.Clear();

                        return null;
                    }

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
