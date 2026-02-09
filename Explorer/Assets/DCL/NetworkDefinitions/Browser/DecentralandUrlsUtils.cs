using CommunicationData.URLHelpers;
using DCL.Optimization.ThreadSafePool;
using UnityEngine.Pool;

namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    public static class DecentralandUrlsUtils
    {
        private static readonly ThreadSafeObjectPool<URLBuilder> URL_BUILDER_POOL
            = new (() => new URLBuilder(), defaultCapacity: 2, actionOnRelease: b => b.Clear());

        public static PooledObject<URLBuilder> BuildFromDomain(this IDecentralandUrlsSource urlsSource, DecentralandUrl domain, out URLBuilder builder)
        {
            PooledObject<URLBuilder> pooled = URL_BUILDER_POOL.Get(out builder);
            builder.AppendDomain(URLDomain.FromString(urlsSource.Url(domain)));
            return pooled;
        }

        public static PooledObject<URLBuilder> BuildFromDomainWithReplacedPath(this IDecentralandUrlsSource urlsSource, DecentralandUrl domain, URLSubdirectory newPath, out URLBuilder builder)
        {
            PooledObject<URLBuilder> pooled = URL_BUILDER_POOL.Get(out builder);
            builder.AppendDomainWithReplacedPath(URLDomain.FromString(urlsSource.Url(domain)), newPath);
            return pooled;
        }

        public static PooledObject<URLBuilder> BuildFromDomain(string domain, out URLBuilder builder) =>
            BuildFromDomain(URLDomain.FromString(domain), out builder);

        public static PooledObject<URLBuilder> BuildFromDomain(URLDomain domain, out URLBuilder builder)
        {
            PooledObject<URLBuilder> pooled = URL_BUILDER_POOL.Get(out builder);
            builder.AppendDomain(domain);
            return pooled;
        }
    }
}
