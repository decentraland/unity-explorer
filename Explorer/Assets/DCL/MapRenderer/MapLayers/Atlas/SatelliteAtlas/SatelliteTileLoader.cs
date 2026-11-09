using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using DCL.Utility.Types;
using DCL.WebRequests;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    /// <summary>
    ///     Reads the KTX2 file of a tile from the disk cache, or else downloads it. A downloaded file is stored only after it decodes.
    ///     The texture keeps no CPU copy.
    /// </summary>
    internal class SatelliteTileLoader : ISatelliteTileLoader
    {
        private const string CACHE_EXTENSION = "ktx2";
        private const int CACHE_ITERATION = 1;

        private readonly IWebRequestController webRequestController;
        private readonly IDiskCache<byte[]> diskCache;
        private readonly CancellationTokenSource lifetimeCts = new ();

        private bool diskCacheFailureReported;

        public SatelliteTileLoader(IWebRequestController webRequestController, IDiskCache<byte[]> diskCache)
        {
            this.webRequestController = webRequestController;
            this.diskCache = diskCache;
        }

        public void Dispose()
        {
            lifetimeCts.SafeCancelAndDispose();
        }

        public async UniTask<Texture2D?> LoadAsync(string url, bool cacheOnly, CancellationToken ct)
        {
            (byte[]? bytes, bool downloaded) = await FetchAsync(url, cacheOnly, ct);

            if (bytes == null)
                return null;

            await UniTask.SwitchToMainThread(ct);
            Texture2D texture;

            try { texture = await KtxTextureDecoder.DecodeAsync(bytes, linear: false, TextureWrapMode.Clamp, FilterMode.Bilinear, readable: false, url); }

            // A missing native decoder is a problem of the machine, not of the file.
            catch (Exception e) when (!downloaded && e is not (OperationCanceledException or DllNotFoundException or EntryPointNotFoundException))
            {
                // Remove the stored file, so that the next load of this URL downloads the file again.
                RemoveCachedFileAsync(url).Forget();
                throw;
            }

            // The write uses the lifetime of the loader, because the token of the load can be cancelled before the write ends.
            if (downloaded && !lifetimeCts.IsCancellationRequested)
                StoreAsync(url, bytes, lifetimeCts.Token).Forget();

            return texture;
        }

        /// <summary>The tile's KTX2 file and whether it was downloaded. Null bytes on a cache miss when <paramref name="cacheOnly" />.</summary>
        private async UniTask<(byte[]? bytes, bool downloaded)> FetchAsync(string url, bool cacheOnly, CancellationToken ct)
        {
            EnumResult<Option<byte[]>, TaskError> cached;

            using (HashKey key = NewCacheKey(url))
                cached = await diskCache.ContentAsync(key, CACHE_EXTENSION, ct);

            ct.ThrowIfCancellationRequested();

            if (cached.Error is { } error)
                ReportDiskCacheFailure(error.Message);
            else if (cached.Value.Has)
                return (cached.Value.Value, false);

            if (cacheOnly)
                return (null, false);

            byte[] bytes = await webRequestController
                                .GetAsync(new CommonArguments(URLAddress.FromString(url), RetryPolicy.WithRetries(1)), ct, ReportCategory.UI, suppressErrors: true)
                                .GetDataCopyAsync();

            return (bytes, true);
        }

        private async UniTaskVoid StoreAsync(string url, byte[] bytes, CancellationToken ct)
        {
            try
            {
                using HashKey key = NewCacheKey(url);
                EnumResult<TaskError> result = await diskCache.PutAsync(key, CACHE_EXTENSION, bytes, ct);

                if (result.Error is { } error)
                    ReportDiskCacheFailure(error.Message);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
        }

        private async UniTaskVoid RemoveCachedFileAsync(string url)
        {
            try
            {
                // A decode can fail after Dispose, and the disposed source's token throws.
                if (lifetimeCts.IsCancellationRequested)
                    return;

                using HashKey key = NewCacheKey(url);
                EnumResult<TaskError> result = await diskCache.RemoveAsync(key, CACHE_EXTENSION, lifetimeCts.Token);

                if (result.Error is { } error)
                    ReportDiskCacheFailure(error.Message);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
        }

        private static HashKey NewCacheKey(string url) =>
            HashKey.FromString($"{CACHE_ITERATION}:{url}");

        /// <summary>Reports only once, because a disabled or failing disk cache affects every tile the same way.</summary>
        private void ReportDiskCacheFailure(string? message)
        {
            if (diskCacheFailureReported)
                return;

            diskCacheFailureReported = true;
            ReportHub.Log(ReportCategory.UI, $"Satellite tiles are not using the disk cache: {message}");
        }
    }
}
