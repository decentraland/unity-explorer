using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace DCL.SDKComponents.MediaStream
{
    public class YouTubeUrlResolver : IYouTubeUrlResolver
    {
        private const int TIMEOUT_MS = 10_000;
        private const float CACHE_TTL_SECONDS = 90f * 60f; // 90 minutes
        private const int PREFERRED_HEIGHT = 1080;

        private readonly YoutubeClient youtubeClient = new ();
        private readonly Dictionary<string, ResolvedYouTubeUrl> cache = new ();

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

            // Single retry on failure
            if (result == null)
            {
                ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] First attempt failed for {videoIdStr}, retrying...");
                result = await TryResolveInternalAsync(videoId.Value, urlHintsLive, ct);
            }

            if (result != null)
                cache[videoIdStr] = result.Value;

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

                IStreamInfo? selectedStream = SelectBestMuxedStream(manifest)
                                              ?? SelectBestAdaptiveVideoStream(manifest);

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

        private static IStreamInfo? SelectBestMuxedStream(StreamManifest manifest)
        {
            return manifest.GetMuxedStreams()
                           .Where(s => s.Container == Container.Mp4)
                           .OrderByDescending(s => s.VideoResolution.Height <= PREFERRED_HEIGHT ? s.VideoResolution.Height : 0)
                           .ThenByDescending(s => s.Bitrate)
                           .FirstOrDefault();
        }

        private static IStreamInfo? SelectBestAdaptiveVideoStream(StreamManifest manifest)
        {
            return manifest.GetVideoOnlyStreams()
                           .Where(s => s.Container == Container.Mp4)
                           .OrderByDescending(s => s.VideoResolution.Height <= PREFERRED_HEIGHT ? s.VideoResolution.Height : 0)
                           .ThenByDescending(s => s.Bitrate)
                           .FirstOrDefault();
        }
    }
}
