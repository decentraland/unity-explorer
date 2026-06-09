using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SDKComponents.MediaStream.YouTube;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;

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

        public YouTubeUrlResolver(IWebRequestController webRequestController)
            : this(new YouTubeVideoClient(webRequestController), () => UnityEngine.Time.realtimeSinceStartup) { }

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
                    return await ResolveHlsAsync(videoId, isLiveStream: true, token);
                }

                // Prefer HLS for VODs too — YouTube's muxed MP4 (itag=18) has known A/V sync
                // problems that don't show up over HLS. Many VOD responses include an HLS
                // manifest URL; if so, use it. Fall back to muxed/video-only selection only
                // when HLS isn't available for this video.
                ResolvedYouTubeUrl? hlsResult = await ResolveHlsAsync(videoId, isLiveStream: false, token);

                if (hlsResult != null)
                    return hlsResult;

                ResolvedYouTubeUrl? vodResult = await TryResolveVodAsync(videoId, token);

                if (vodResult != null)
                    return vodResult;

                ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{TAG}] VOD/HLS both failed for {videoId}, last-resort live HLS retry...");
                return await ResolveHlsAsync(videoId, isLiveStream: true, token);
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

        private async UniTask<ResolvedYouTubeUrl?> ResolveHlsAsync(VideoId videoId, bool isLiveStream, CancellationToken token)
        {
            string hlsUrl = await youtubeClient.GetStreamingManifestUrlAsync(videoId, token);

            if (string.IsNullOrEmpty(hlsUrl))
            {
                // Live: warn (HLS is required for live). VOD: log at info — the muxed fallback will run.
                if (isLiveStream)
                    ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[{TAG}] No HLS manifest found for live stream {videoId}");
                else
                    ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{TAG}] No HLS manifest for VOD {videoId}, falling back to muxed MP4");

                return null;
            }

            ReportHub.Log(ReportCategory.MEDIA_STREAM,
                $"[{TAG}] Resolved {(isLiveStream ? "live stream" : "VOD")} {videoId} to streaming manifest");

            return new ResolvedYouTubeUrl(
                hlsUrl,
                isLiveStream: isLiveStream,
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

                // Muxed MP4 (typically itag=18) is YouTube's legacy compatibility format; some
                // videos (embed-restricted music videos especially) only expose this format and
                // not HLS/DASH manifests. AVPro can have minor A/V sync drift on these — that's
                // a YouTube-side limitation, not something our resolver can fix. Logged at
                // Warning so operators notice when a video falls into this degraded path.
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM,
                    $"[{TAG}] Resolved VOD {videoId} to muxed {selectedStream.Container} " +
                    $"{(selectedStream is IVideoStreamInfo vs ? $"{vs.VideoResolution.Width}x{vs.VideoResolution.Height}" : "audio-only")} (no HLS/DASH manifest available — may have A/V sync drift)");

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
