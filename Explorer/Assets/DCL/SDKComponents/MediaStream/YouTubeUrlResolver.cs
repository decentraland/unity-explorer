using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace DCL.SDKComponents.MediaStream
{
    public class YouTubeUrlResolver
    {
        private const int TIMEOUT_MS = 10_000;
        private const int RETRY_BACKOFF_MS = 1_000;

        // YouTube stream URLs typically expire after 2-4 hours.
        // 90 minutes is a conservative TTL that ensures re-resolution before expiry.
        private const float CACHE_TTL_SECONDS = 90f * 60f;

        private const int PREFERRED_HEIGHT = 1080;
        private const int MAX_CACHE_ENTRIES = 50;

        private readonly YoutubeClient youtubeClient = new ();
        private readonly Dictionary<string, ResolvedYouTubeUrl> cache = new ();
        private readonly List<string> expiredKeys = new ();

        public async UniTask<ResolvedYouTubeUrl?> ResolveAsync(string youtubeUrl, CancellationToken ct)
        {
            VideoId? videoId = VideoId.TryParse(youtubeUrl);

            if (videoId == null)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] Could not parse video ID from URL: {youtubeUrl}");
                return null;
            }

            string videoIdStr = videoId.Value.Value;

            if (TryGetCached(videoIdStr, out ResolvedYouTubeUrl cached))
                return cached;

            bool urlHintsLive = youtubeUrl.Contains("/live/");

            // First attempt
            ResolvedYouTubeUrl? result = await TryResolveInternalAsync(videoId.Value, urlHintsLive, ct);

            // Single retry with backoff
            if (result == null)
            {
                ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] First attempt failed for {videoIdStr}, retrying after {RETRY_BACKOFF_MS}ms...");
                await UniTask.Delay(RETRY_BACKOFF_MS, cancellationToken: ct);
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
                if (UnityEngine.Time.realtimeSinceStartup < cached.ExpiresAtRealtimeSinceStartup)
                {
                    ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] Cache hit for {videoId}");
                    return true;
                }

                cache.Remove(videoId);
            }

            return false;
        }

        private void EvictEntriesToMaintainCap()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;

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
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TIMEOUT_MS);
            CancellationToken token = timeoutCts.Token;

            try
            {
                bool isLive = urlHintsLive;

                if (!isLive)
                {
                    Video video = await youtubeClient.Videos.GetAsync(videoId, token);
                    isLive = video.Duration == null;
                }

                if (isLive)
                {
                    ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] Detected live stream: {videoId}");
                    return await ResolveLiveStreamAsync(videoId, token);
                }

                // Try VOD first, fall back to live stream if it fails
                ResolvedYouTubeUrl? vodResult = await TryResolveVodAsync(videoId, token);

                if (vodResult != null)
                    return vodResult;

                ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] VOD failed for {videoId}, trying live stream path...");
                return await ResolveLiveStreamAsync(videoId, token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // External cancellation — propagate
                throw;
            }
            catch (OperationCanceledException)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] Timeout resolving {videoId}");
                return null;
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] Failed to resolve {videoId}: {ex.Message}");
                return null;
            }
        }

        private async UniTask<ResolvedYouTubeUrl?> ResolveLiveStreamAsync(VideoId videoId, CancellationToken token)
        {
            string hlsUrl = await youtubeClient.Videos.Streams.GetHttpLiveStreamUrlAsync(videoId, token);

            if (string.IsNullOrEmpty(hlsUrl))
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] No HLS manifest found for live stream {videoId}");
                return null;
            }

            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] Resolved live stream {videoId} to HLS manifest");

            return new ResolvedYouTubeUrl(
                hlsUrl,
                isLiveStream: true,
                UnityEngine.Time.realtimeSinceStartup + CACHE_TTL_SECONDS
            );
        }

        private async UniTask<ResolvedYouTubeUrl?> TryResolveVodAsync(VideoId videoId, CancellationToken token)
        {
            try
            {
                StreamManifest manifest = await youtubeClient.Videos.Streams.GetManifestAsync(videoId, token);

                IStreamInfo selectedStream = SelectBestStream(manifest.GetMuxedStreams())
                                             ?? SelectBestStream(manifest.GetVideoOnlyStreams());

                if (selectedStream == null)
                {
                    ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] No suitable stream found for {videoId}");
                    return null;
                }

                ReportHub.Log(ReportCategory.MEDIA_STREAM,
                    $"[YouTubeResolver] Resolved VOD {videoId}: {selectedStream.Container} " +
                    $"{(selectedStream is IVideoStreamInfo vs ? $"{vs.VideoResolution.Width}x{vs.VideoResolution.Height}" : "audio-only")}");

                return new ResolvedYouTubeUrl(
                    selectedStream.Url,
                    isLiveStream: false,
                    UnityEngine.Time.realtimeSinceStartup + CACHE_TTL_SECONDS
                );
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] VOD resolution failed for {videoId}: {ex.Message}");
                return null;
            }
        }

        private static IStreamInfo SelectBestStream(IEnumerable<IStreamInfo> streams)
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
