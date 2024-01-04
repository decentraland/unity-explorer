namespace DCL.Web3Authentication
{
    public class MemoryWeb3IdentityCache : IWeb3IdentityCache
    {
        public IWeb3Identity? Identity { get; set; }

        public void Dispose()
        {
            Identity?.Dispose();
        }

        public void Clear()
        {
            Identity = null;
        }
    }
}
