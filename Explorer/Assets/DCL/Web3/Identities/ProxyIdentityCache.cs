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

        public IWeb3Identity? Identity
        {
            get
            {
                if (memory.Identity != null)
                    return memory.Identity;

                IWeb3Identity? storedIdentity = storage.Identity;

                if (storedIdentity != null)
                    memory.Identity = storedIdentity;

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
