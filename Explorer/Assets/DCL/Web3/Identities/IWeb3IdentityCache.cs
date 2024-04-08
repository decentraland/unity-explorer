using System;

namespace DCL.Web3.Identities
{
    public interface IWeb3IdentityCache : IDisposable
    {
        IWeb3Identity? Identity { get; set; }

        void Clear();

        class Fake : IWeb3IdentityCache
        {
            private readonly IWeb3Identity? identity;

            public Fake() : this(new IWeb3Identity.Random()) { }

            public Fake(IWeb3Identity? identity)
            {
                this.identity = identity;
            }

            public IWeb3Identity? Identity
            {
                get => identity;

                set
                {
                    //ignore
                }
            }

            public void Clear()
            {
                //ignore
            }

            public void Dispose()
            {
                //ignore
            }
        }

        class Default : IWeb3IdentityCache
        {
            private readonly IWeb3IdentityCache origin;

            public Default()
            {
                origin = new ProxyIdentityCache(
                    new MemoryWeb3IdentityCache(),
                    new PlayerPrefsIdentityProvider(
                        new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()
                    )
                );
            }

            public IWeb3Identity? Identity
            {
                get => origin.Identity;
                set => origin.Identity = value;
            }

            public void Clear()
            {
                origin.Clear();
            }

            public void Dispose()
            {
                origin.Dispose();
            }
        }
    }

    public static class Web3IdentityCacheExtensions
    {
        public static IWeb3Identity EnsuredIdentity(this IWeb3IdentityCache cache, string errorMessage = "Identity is not found in the cache")
        {
            if (cache.Identity == null)
                throw new InvalidOperationException(errorMessage);

            return cache.Identity;
        }
    }
}
