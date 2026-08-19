using Cysharp.Threading.Tasks;
using DCL.AvProSwitch;
using DCL.Diagnostics;
using DCL.SDKComponents.MediaStream.YouTube;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.WebRequests;
using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SDKComponents.MediaStream
{
    internal interface IYouTubeVideoClient
    {
        /// <summary>Returns true if the video is a live stream.</summary>
        UniTask<bool> IsLiveStreamAsync(VideoId videoId, CancellationToken ct);

        UniTask<StreamManifest> GetStreamManifestAsync(VideoId videoId, CancellationToken ct);

        /// <summary>
        ///     Returns a streaming manifest URL for the given video. Resolution order:
        ///     1. YouTube's native HLS manifest (if returned)
        ///     2. YouTube's native DASH manifest — only when the active playback backend can
        ///        demux DASH (UUAV's FFmpeg, or AVPro's WinRT path on Windows; AVFoundation
        ///        on macOS cannot).
        ///     3. For VODs — a locally synthesized HLS multivariant playlist. Fixes A/V sync
        ///        on embed-restricted videos that only get muxed itag=18, and keeps their
        ///        quality above itag=18's ~360p. Shape depends on the backend: AVPro gets it
        ///        written to <see cref="Application.temporaryCachePath"/> as a <c>file://</c>
        ///        URL; UUAV gets a loopback HTTP URL served by
        ///        <see cref="LocalHlsPlaylistServer"/> (its sandboxed helper deliberately
        ///        refuses the <c>file</c> protocol — media URLs come from untrusted scenes).
        ///     4. Empty string — no manifest playable by the active backend.
        /// </summary>
        UniTask<string> GetStreamingManifestUrlAsync(VideoId videoId, CancellationToken ct);
    }

    internal class YouTubeVideoClient : IYouTubeVideoClient
    {
        private const string TAG = nameof(YouTubeVideoClient);
        private const string SYNTH_HLS_DIR_PREFIX = "youtube_hls_";

        // Balances few, large requests (see HlsManifestBuilder.Coalesce) against seek granularity.
        private const float UUAV_TARGET_SEGMENT_SECONDS = 10f;

        private readonly InnerTubeClient innerTube = new ();
        private readonly IWebRequestController webRequestController;

        public YouTubeVideoClient(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<bool> IsLiveStreamAsync(VideoId videoId, CancellationToken ct)
        {
            PlayerResponse response = await innerTube.FetchPlayerResponseAsync(videoId, ct);
            return response.IsLive;
        }

        public async UniTask<StreamManifest> GetStreamManifestAsync(VideoId videoId, CancellationToken ct)
        {
            PlayerResponse response = await innerTube.FetchPlayerResponseAsync(videoId, ct);
            return new StreamManifest(response.MuxedStreams, response.VideoOnlyStreams);
        }

        public async UniTask<string> GetStreamingManifestUrlAsync(VideoId videoId, CancellationToken ct)
        {
            PlayerResponse response = await innerTube.FetchPlayerResponseAsync(videoId, ct);

            // Native HLS — preferred: both backends demux it on every platform.
            if (!string.IsNullOrEmpty(response.HlsManifestUrl))
                return response.HlsManifestUrl!;

            if (!string.IsNullOrEmpty(response.DashManifestUrl) && CurrentBackendSupportsDash())
                return response.DashManifestUrl!;

            // Synthesized HLS fallback — only for VODs. Live streams without HLS are unplayable
            // here (they'd need a different live format) so we don't synthesize for them.
            if (!response.IsLive && response.AdaptiveFormats.Count > 0)
            {
                string? synthesizedUrl = await TrySynthesizeHlsAsync(videoId, response, ct);

                if (!string.IsNullOrEmpty(synthesizedUrl))
                    return synthesizedUrl;
            }

            return string.Empty;
        }

        // DASH demuxing capability of the active playback backend:
        // - UUAV: FFmpeg's dash demuxer, present in both shipped builds (requires libxml2).
        // - AVPro: only the WinRT video API on Windows; AVFoundation (macOS) has no DASH
        //   support. Windows answers true unconditionally — diagnostics setups that force
        //   Media Foundation are knowingly ignored.
        private static bool CurrentBackendSupportsDash()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return true;
#else
            return MediaPlayerBackendSelection.UseCustomPlayer;
#endif
        }

        /// <summary>
        ///     Generates an HLS multivariant playlist (master + video + audio) from the response's
        ///     adaptive streams and returns a URL the active backend can open: a <c>file://</c>
        ///     master written into a per-video subdirectory of the temp cache for AVPro, or a
        ///     loopback HTTP URL for UUAV. Returns null on any failure (no usable streams,
        ///     write error, no free loopback port, etc.).
        ///
        ///     Pre-fetches the sidx (segment index) box for the selected video and audio streams
        ///     via byte-range HTTP requests; that lets the playlist enumerate one HLS segment per
        ///     fmp4 fragment instead of a single segment over the entire file, so playback can
        ///     start after the first ~5-10s chunk rather than the whole body (issue #8350). If
        ///     sidx fetch or parse fails, falls back to single-segment.
        ///
        ///     Deliberately contains no try/catch/using around its await: an exception unwinding
        ///     through this async state machine's catch and finally funclets crashes IL2CPP on
        ///     Windows inside catch-clause matching (Sentry UNITY-EXPLORER-P77). The fetch is
        ///     therefore exception-free by contract and everything that can throw lives in the
        ///     synchronous <see cref="SynthesizeHls"/>.
        /// </summary>
        private async UniTask<string?> TrySynthesizeHlsAsync(VideoId videoId, PlayerResponse response, CancellationToken ct)
        {
            if (response.DurationSeconds <= 0
                || !HlsManifestBuilder.TrySelectVideoAndAudio(response.AdaptiveFormats, out AdaptiveFormatData videoStream, out AdaptiveFormatData audioStream))
            {
                ReportHub.Log(ReportCategory.MEDIA_STREAM,
                    $"[{TAG}] HLS synthesis skipped for {videoId.Value} — no usable mp4 video+audio adaptive pair");
                return null;
            }

            // Fetch both sidx boxes in parallel. Each is typically a few KB.
            (Result<byte[]> videoSidx, Result<byte[]> audioSidx) = await UniTask.WhenAll(
                FetchByteRangeAsync(videoStream.Url, videoStream.IndexRangeStart, videoStream.IndexRangeEnd, ct),
                FetchByteRangeAsync(audioStream.Url, audioStream.IndexRangeStart, audioStream.IndexRangeEnd, ct));

            if (ct.IsCancellationRequested)
                return null;

            return SynthesizeHls(videoId, videoStream, audioStream, response.DurationSeconds, videoSidx, audioSidx);
        }

        /// <summary>
        ///     Fetches an inclusive byte range from <paramref name="url"/> via the project's
        ///     <see cref="IWebRequestController"/>, using the built-in <c>Range</c> header support.
        ///     Never throws — failures and cancellation both surface as an unsuccessful
        ///     <see cref="Result{T}" /> so no exception crosses the <c>UniTask.WhenAll</c> boundary.
        /// </summary>
        private UniTask<Result<byte[]>> FetchByteRangeAsync(string url, long start, long endInclusive, CancellationToken ct) =>
            webRequestController
               .GetAsync(url, ct, ReportCategory.MEDIA_STREAM,
                    headersInfo: new WebRequestHeadersInfo().WithRange(start, endInclusive),
                    suppressErrors: true)
               .GetDataCopyAsync()
                // Fixes: https://github.com/decentraland/unity-explorer/issues/9758
               .SuppressToResultAsync();

        /// <summary>
        ///     Parses the pre-fetched sidx boxes, builds the playlists and returns the URL for
        ///     the active backend: the absolute <c>file://</c> master written to the temp cache
        ///     for AVPro, or a loopback HTTP URL for UUAV. Returns null if any step fails.
        ///     Synchronous on purpose — see <see cref="TrySynthesizeHlsAsync"/>.
        /// </summary>
        private static string? SynthesizeHls(
            VideoId videoId,
            AdaptiveFormatData videoStream,
            AdaptiveFormatData audioStream,
            int durationSeconds,
            Result<byte[]> videoSidx,
            Result<byte[]> audioSidx)
        {
            try
            {
                using var _ = ListPool<SidxParser.SegmentInfo>.Get(out var videoSegments);
                using var __ = ListPool<SidxParser.SegmentInfo>.Get(out var audioSegments);

                if (videoSidx.Success)
                    SidxParser.TryParse(videoSidx.Value, videoStream.IndexRangeEnd + 1, videoSegments);
                else
                    ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{TAG}] video sidx byte-range fetch failed: {videoSidx.ErrorMessage}");

                if (audioSidx.Success)
                    SidxParser.TryParse(audioSidx.Value, audioStream.IndexRangeEnd + 1, audioSegments);
                else
                    ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{TAG}] audio sidx byte-range fetch failed: {audioSidx.ErrorMessage}");

                if (MediaPlayerBackendSelection.UseCustomPlayer)
                {
                    // The UUAV helper refuses the file protocol, so its playlists are served
                    // over loopback HTTP. Coalescing costs no startup latency on this backend:
                    // FFmpeg streams a byte range progressively.
                    HlsManifestBuilder.PlaylistSet loopbackPlaylists = HlsManifestBuilder.Build(
                        videoStream, audioStream, durationSeconds, videoSegments, audioSegments, UUAV_TARGET_SEGMENT_SECONDS);

                    string? masterUrl = LocalHlsPlaylistServer.TryRegister(videoId.Value, loopbackPlaylists);

                    if (masterUrl != null)
                        ReportHub.Log(ReportCategory.MEDIA_STREAM,
                            $"[{TAG}] Synthesized loopback HLS playlist for {videoId.Value} at {masterUrl}");

                    return masterUrl;
                }

                HlsManifestBuilder.PlaylistSet playlists =
                    HlsManifestBuilder.Build(videoStream, audioStream, durationSeconds, videoSegments, audioSegments);

                // Per-video subdirectory keeps the 3 files together so the master playlist's
                // relative URIs (audio.m3u8, video.m3u8) resolve correctly. Unity's
                // temporaryCachePath is OS-cleaned so we don't need to garbage-collect.
                string playlistDir = Path.Combine(Application.temporaryCachePath, SYNTH_HLS_DIR_PREFIX + videoId.Value);
                Directory.CreateDirectory(playlistDir);

                File.WriteAllText(Path.Combine(playlistDir, HlsManifestBuilder.VIDEO_PLAYLIST_NAME), playlists.Video, HlsManifestBuilder.HLS_ENCODING);
                File.WriteAllText(Path.Combine(playlistDir, HlsManifestBuilder.AUDIO_PLAYLIST_NAME), playlists.Audio, HlsManifestBuilder.HLS_ENCODING);

                string masterPath = Path.Combine(playlistDir, HlsManifestBuilder.MASTER_PLAYLIST_NAME);
                File.WriteAllText(masterPath, playlists.Master, HlsManifestBuilder.HLS_ENCODING);

                ReportHub.Log(ReportCategory.MEDIA_STREAM,
                    $"[{TAG}] Synthesized HLS playlist for {videoId.Value} at {masterPath}");

                return "file://" + masterPath;
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM,
                    $"[{TAG}] HLS synthesis failed for {videoId.Value}: {ex.Message}");
                return null;
            }
        }
    }
}
