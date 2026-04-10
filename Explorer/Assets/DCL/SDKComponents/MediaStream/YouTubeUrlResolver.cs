using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace DCL.SDKComponents.MediaStream
{
    public class YouTubeUrlResolver : IYouTubeUrlResolver
    {
        private static readonly string TAG = nameof(YouTubeUrlResolver);

        private const int TIMEOUT_MS = 10_000;
        private const int RETRY_BACKOFF_MS = 1_000;

        // YouTube stream URLs typically expire after 2-4 hours.
        // 90 minutes is a conservative TTL that ensures re-resolution before expiry.
        internal const float CACHE_TTL_SECONDS = 90f * 60f;

        private const int PREFERRED_HEIGHT = 1080;
        private const int MAX_CACHE_ENTRIES = 50;

        private readonly IYouTubeVideoClient youtubeClient;
        private readonly Func<float> getRealtimeSinceStartup;
        private readonly Dictionary<string, ResolvedYouTubeUrl> cache = new ();
        private readonly List<string> expiredKeys = new ();

        public YouTubeUrlResolver() : this(new YoutubeClientAdapter(), () => UnityEngine.Time.realtimeSinceStartup) { }

        internal YouTubeUrlResolver(IYouTubeVideoClient client, Func<float> getRealtimeSinceStartup)
        {
            youtubeClient = client;
            this.getRealtimeSinceStartup = getRealtimeSinceStartup;
        }

        public async UniTask<ResolvedYouTubeUrl?> ResolveAsync(string youtubeUrl, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return null;

            VideoId? videoId = VideoId.TryParse(youtubeUrl);

            if (videoId == null)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[{TAG}] Could not parse video ID from URL: {youtubeUrl}");
                return null;
            }

            string videoIdStr = videoId.Value.Value;

            if (TryGetCached(videoIdStr, out ResolvedYouTubeUrl cached))
                return cached;

            bool urlHintsLive = youtubeUrl.Contains("/live/");

            // First attempt
            ResolvedYouTubeUrl? result = await TryResolveInternalAsync(videoId.Value, urlHintsLive, ct);

            // Single retry with backoff
            if (result == null && !ct.IsCancellationRequested)
            {
                ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{TAG}] First attempt failed for {videoIdStr}, retrying after {RETRY_BACKOFF_MS}ms...");
                await UniTask.Delay(RETRY_BACKOFF_MS, cancellationToken: ct);

                if (!ct.IsCancellationRequested)
                    result = await TryResolveInternalAsync(videoId.Value, urlHintsLive, ct);
            }

            if (result != null)
            {
                if (cache.Count >= MAX_CACHE_ENTRIES)
                    EvictEntriesToMaintainCap();

                cache[videoIdStr] = result.Value;
            }

            return result;
        }

        private bool TryGetCached(string videoId, out ResolvedYouTubeUrl cached)
        {
            if (cache.TryGetValue(videoId, out cached))
            {
                if (getRealtimeSinceStartup() < cached.ExpiresAtRealtimeSinceStartup)
                {
                    ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{TAG}] Cache hit for {videoId}");
                    return true;
                }

                cache.Remove(videoId);
            }

            return false;
        }

        private void EvictEntriesToMaintainCap()
        {
            float now = getRealtimeSinceStartup();

            // First pass: remove expired entries
            expiredKeys.Clear();

            foreach (var kvp in cache)
                if (now >= kvp.Value.ExpiresAtRealtimeSinceStartup)
                    expiredKeys.Add(kvp.Key);

            foreach (string key in expiredKeys)
                cache.Remove(key);

            // If still at cap, remove the entry with earliest expiry
            while (cache.Count >= MAX_CACHE_ENTRIES)
            {
                string oldestKey = null;
                float oldestExpiry = float.MaxValue;

                foreach (var kvp in cache)
                {
                    if (kvp.Value.ExpiresAtRealtimeSinceStartup < oldestExpiry)
                    {
                        oldestExpiry = kvp.Value.ExpiresAtRealtimeSinceStartup;
                        oldestKey = kvp.Key;
                    }
                }

                if (oldestKey != null)
                    cache.Remove(oldestKey);
                else
                    break;
            }
        }

        private async UniTask<ResolvedYouTubeUrl?> TryResolveInternalAsync(VideoId videoId, bool urlHintsLive, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return null;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TIMEOUT_MS);
            CancellationToken token = timeoutCts.Token;

            try
            {
                bool isLive = urlHintsLive;

                if (!isLive)
                    isLive = await youtubeClient.IsLiveStreamAsync(videoId, token);

                if (isLive)
                {
                    ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{TAG}] Detected live stream: {videoId}");
                    return await ResolveLiveStreamAsync(videoId, token);
                }

                // Try VOD first, fall back to live stream if it fails
                ResolvedYouTubeUrl? vodResult = await TryResolveVodAsync(videoId, token);

                if (vodResult != null)
                    return vodResult;

                ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{TAG}] VOD failed for {videoId}, trying live stream path...");
                return await ResolveLiveStreamAsync(videoId, token);
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                    ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{TAG}] Resolution cancelled for {videoId}");
                else
                    ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[{TAG}] Timeout resolving {videoId}");

                return null;
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.MEDIA_STREAM, $"[{TAG}] Failed to resolve {videoId}: {ex.Message}");
                return null;
            }
        }

        private async UniTask<ResolvedYouTubeUrl?> ResolveLiveStreamAsync(VideoId videoId, CancellationToken token)
        {
            string hlsUrl = await youtubeClient.GetHttpLiveStreamUrlAsync(videoId, token);

            if (string.IsNullOrEmpty(hlsUrl))
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[{TAG}] No HLS manifest found for live stream {videoId}");
                return null;
            }

            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{TAG}] Resolved live stream {videoId} to HLS manifest");

            return new ResolvedYouTubeUrl(
                hlsUrl,
                isLiveStream: true,
                getRealtimeSinceStartup() + CACHE_TTL_SECONDS
            );
        }

        private async UniTask<ResolvedYouTubeUrl?> TryResolveVodAsync(VideoId videoId, CancellationToken token)
        {
            try
            {
                StreamManifest manifest = await youtubeClient.GetStreamManifestAsync(videoId, token);

                IStreamInfo selectedStream = SelectBestStream(manifest.GetMuxedStreams())
                                             ?? SelectBestStream(manifest.GetVideoOnlyStreams());

                if (selectedStream == null)
                {
                    ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[{TAG}] No suitable stream found for {videoId}");
                    return null;
                }

                ReportHub.Log(ReportCategory.MEDIA_STREAM,
                    $"[{TAG}] Resolved VOD {videoId}: {selectedStream.Container} " +
                    $"{(selectedStream is IVideoStreamInfo vs ? $"{vs.VideoResolution.Width}x{vs.VideoResolution.Height}" : "audio-only")}");

                return new ResolvedYouTubeUrl(
                    selectedStream.Url,
                    isLiveStream: false,
                    getRealtimeSinceStartup() + CACHE_TTL_SECONDS
                );
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{TAG}] VOD resolution cancelled for {videoId}");
                return null;
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[{TAG}] VOD resolution failed for {videoId}: {ex.Message}");
                return null;
            }
        }

        internal static IStreamInfo SelectBestStream(IEnumerable<IStreamInfo> streams)
        {
            IStreamInfo best = null;
            int bestHeight = -1;
            long bestBitrate = -1;

            foreach (IStreamInfo stream in streams)
            {
                if (stream.Container != Container.Mp4) continue;
                if (stream is not IVideoStreamInfo videoStream) continue;

                int height = videoStream.VideoResolution.Height <= PREFERRED_HEIGHT
                    ? videoStream.VideoResolution.Height
                    : 0;

                if (height > bestHeight || (height == bestHeight && stream.Bitrate.BitsPerSecond > bestBitrate))
                {
                    best = stream;
                    bestHeight = height;
                    bestBitrate = stream.Bitrate.BitsPerSecond;
                }
            }

            return best;
        }
    }
}
