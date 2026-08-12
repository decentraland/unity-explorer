using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PrivateWorlds
{
    /// <summary>
    /// Decorates an <see cref="IWorldPermissionsService"/> with a short-TTL cache for CheckWorldAccessAsync so that
    /// recycling a private-world card in and out of view (SuperScrollView) or re-opening its detail panel does not
    /// re-fire a network permission fetch every time. The cache key includes the current wallet address, so an
    /// identity switch is an automatic miss and access can never be shown stale across a wallet change. Transient
    /// CheckFailed results are never cached (they stay retryable), and a successful password validation invalidates
    /// every cached entry for that world.
    /// </summary>
    public class CachedWorldPermissionsService : IWorldPermissionsService
    {
        private readonly IWorldPermissionsService inner;
        private readonly Func<string?> currentWalletGetter;
        private readonly TimeSpan ttl;
        private readonly Func<float> now;
        private readonly Dictionary<string, (float cachedAtRealtime, WorldAccessCheckContext ctx)> cache = new ();

        public CachedWorldPermissionsService(
            IWorldPermissionsService inner,
            Func<string?> currentWalletGetter,
            TimeSpan ttl,
            Func<float>? now = null)
        {
            this.inner = inner;
            this.currentWalletGetter = currentWalletGetter;
            this.ttl = ttl;
            this.now = now ?? (() => UnityEngine.Time.realtimeSinceStartup);
        }

        public async UniTask<WorldAccessCheckContext> CheckWorldAccessAsync(string worldName, CancellationToken ct)
        {
            string key = BuildKey(worldName);

            if (cache.TryGetValue(key, out var entry) && now() - entry.cachedAtRealtime < ttl.TotalSeconds)
                return entry.ctx;

            WorldAccessCheckContext context = await inner.CheckWorldAccessAsync(worldName, ct);

            if (context.Result != WorldAccessCheckResult.CheckFailed)
                cache[key] = (now(), context);

            return context;
        }

        public UniTask<WorldAccessInfo> GetWorldPermissionsAsync(string worldName, CancellationToken ct) =>
            inner.GetWorldPermissionsAsync(worldName, ct);

        public async UniTask<ValidatePasswordResult> ValidatePasswordAsync(string worldName, string password, CancellationToken ct)
        {
            ValidatePasswordResult result = await inner.ValidatePasswordAsync(worldName, password, ct);

            if (result.Success)
            {
                string prefix = NormalizeWorldName(worldName) + '|';
                List<string>? toRemove = null;
                foreach (string key in cache.Keys)
                    if (key.StartsWith(prefix, StringComparison.Ordinal))
                        (toRemove ??= new List<string>()).Add(key);

                if (toRemove != null)
                    foreach (string key in toRemove)
                        cache.Remove(key);
            }

            return result;
        }

        private string BuildKey(string worldName) =>
            $"{NormalizeWorldName(worldName)}|{(currentWalletGetter() ?? string.Empty).ToLowerInvariant()}";

        private static string NormalizeWorldName(string worldName) =>
            worldName.Trim().ToLowerInvariant();
    }
}
