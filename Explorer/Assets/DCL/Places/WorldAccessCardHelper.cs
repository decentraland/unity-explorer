using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PrivateWorlds;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Places
{
    /// <summary>
    /// Shared helper for checking world access and updating place card UI.
    /// Used by PlacesResultsView and PlacesSectionView to avoid duplication.
    /// </summary>
    public static class WorldAccessCardHelper
    {
        private static readonly TimeSpan CACHE_TTL = TimeSpan.FromSeconds(10);
        private static readonly Dictionary<string, CacheEntry> cache = new (StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, UniTaskCompletionSource<WorldAccessCheckContext>> inFlight = new (StringComparer.OrdinalIgnoreCase);

        private readonly struct CacheEntry
        {
            public readonly WorldAccessCheckContext Context;
            public readonly DateTime ExpiresAtUtc;

            public CacheEntry(WorldAccessCheckContext context, DateTime expiresAtUtc)
            {
                Context = context;
                ExpiresAtUtc = expiresAtUtc;
            }
        }

        public static async UniTaskVoid CheckAndUpdateCardAsync(
            IWorldPermissionsService worldPermissionsService,
            string worldName,
            PlaceCardView cardView,
            CancellationToken ct)
        {
            try
            {
                WorldAccessCheckContext context = await GetOrFetchContextAsync(worldPermissionsService, worldName, ct);
                cardView.SetWorldAccessState(context.Result, context.AccessInfo?.AccessType);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM, $"Failed to check world access for '{worldName}': {e.Message}");
            }
        }

        private static async UniTask<WorldAccessCheckContext> GetOrFetchContextAsync(
            IWorldPermissionsService worldPermissionsService,
            string worldName,
            CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            string key = worldName.Trim();
            DateTime now = DateTime.UtcNow;

            if (TryGetCachedContext(key, now, out WorldAccessCheckContext cached))
                return cached;

            if (!inFlight.TryGetValue(key, out UniTaskCompletionSource<WorldAccessCheckContext>? tcs))
            {
                tcs = new UniTaskCompletionSource<WorldAccessCheckContext>();
                inFlight[key] = tcs;
                ResolveInFlightRequestAsync(worldPermissionsService, key, tcs).Forget();
            }

            return await tcs.Task.AttachExternalCancellation(ct);
        }

        private static bool TryGetCachedContext(string key, DateTime nowUtc, out WorldAccessCheckContext context)
        {
            if (cache.TryGetValue(key, out CacheEntry entry))
            {
                if (entry.ExpiresAtUtc > nowUtc)
                {
                    context = entry.Context;
                    return true;
                }

                cache.Remove(key);
            }

            context = default;
            return false;
        }

        private static async UniTaskVoid ResolveInFlightRequestAsync(
            IWorldPermissionsService worldPermissionsService,
            string key,
            UniTaskCompletionSource<WorldAccessCheckContext> tcs)
        {
            WorldAccessCheckContext context;

            try
            {
                context = await worldPermissionsService.CheckWorldAccessAsync(key, CancellationToken.None);
            }
            catch (Exception e)
            {
                context = new WorldAccessCheckContext
                {
                    Result = WorldAccessCheckResult.CheckFailed,
                    ErrorMessage = e.Message
                };
            }

            await UniTask.SwitchToMainThread();

            if (context.Result != WorldAccessCheckResult.CheckFailed)
                cache[key] = new CacheEntry(context, DateTime.UtcNow + CACHE_TTL);

            inFlight.Remove(key);
            tcs.TrySetResult(context);
        }
    }
}
