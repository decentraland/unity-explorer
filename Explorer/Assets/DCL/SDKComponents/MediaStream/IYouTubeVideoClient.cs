using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SDKComponents.MediaStream.YouTube;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.WebRequests;
using System;
using System.IO;
using System.Text;
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
        ///     2. YouTube's native DASH manifest (if returned)
        ///     3. For VODs only — a locally synthesized HLS multivariant playlist written to
        ///        <see cref="Application.temporaryCachePath"/> and exposed as a <c>file://</c>
        ///        URL. Fixes A/V sync on embed-restricted videos that only get muxed itag=18.
        ///        HLS chosen over DASH because every AVPro backend (AVFoundation/MediaFoundation/
        ///        ExoPlayer) supports it natively; DASH support varies by platform.
        ///     4. Empty string — caller falls through to muxed-MP4 selection.
        /// </summary>
        UniTask<string> GetStreamingManifestUrlAsync(VideoId videoId, CancellationToken ct);
    }

    internal class YouTubeVideoClient : IYouTubeVideoClient
    {
        private const string TAG = nameof(YouTubeVideoClient);
        private const string SYNTH_HLS_DIR_PREFIX = "youtube_hls_";
        private const string MASTER_PLAYLIST_NAME = "master.m3u8";
        private const string VIDEO_PLAYLIST_NAME = "video.m3u8";
        private const string AUDIO_PLAYLIST_NAME = "audio.m3u8";

        // HLS spec requires UTF-8 without BOM (RFC 8216 §4).
        private static readonly UTF8Encoding HLS_ENCODING = new (encoderShouldEmitUTF8Identifier: false);

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

            // Native HLS — preferred (rock-solid AVPro support across all platforms).
            if (!string.IsNullOrEmpty(response.HlsManifestUrl))
                return response.HlsManifestUrl!;

            // Native DASH — works on AVPro Pro on Windows/Android, limited on macOS.
            if (!string.IsNullOrEmpty(response.DashManifestUrl))
                return response.DashManifestUrl!;

            // Synthesized HLS fallback — only for VODs. Live streams without HLS are unplayable
            // here (they'd need a different live format) so we don't synthesize for them.
            if (!response.IsLive && response.AdaptiveFormats.Count > 0)
            {
                string? synthesizedPath = await TryWriteSynthesizedHlsAsync(videoId, response, ct);

                if (!string.IsNullOrEmpty(synthesizedPath))
                {
                    ReportHub.Log(ReportCategory.MEDIA_STREAM,
                        $"[{TAG}] Synthesized HLS playlist for {videoId.Value} at {synthesizedPath}");

                    return "file://" + synthesizedPath;
                }
            }

            return string.Empty;
        }

        /// <summary>
        ///     Generates an HLS multivariant playlist (master + video + audio) from the response's
        ///     adaptive streams, writes the 3 files into a per-video subdirectory of the temp
        ///     cache, and returns the absolute path of the master playlist. Returns null on any
        ///     failure (no usable streams, write error, etc.) so the caller falls through to the
        ///     muxed path.
        ///
        ///     Pre-fetches the sidx (segment index) box for the selected video and audio streams
        ///     via byte-range HTTP requests; that lets the playlist enumerate one HLS segment per
        ///     fmp4 fragment instead of a single segment over the entire file. AVPro can then
        ///     start playback after fetching the first ~5-10s chunk rather than the whole body
        ///     (issue #8350). If sidx fetch or parse fails, falls back to single-segment.
        ///
        ///     Deliberately contains no try/catch/using around its await: an exception unwinding
        ///     through this async state machine's catch and finally funclets crashes IL2CPP on
        ///     Windows inside catch-clause matching (Sentry UNITY-EXPLORER-P77). The fetch is
        ///     therefore exception-free by contract and everything that can throw lives in the
        ///     synchronous <see cref="WriteSynthesizedHls"/>.
        /// </summary>
        private async UniTask<string?> TryWriteSynthesizedHlsAsync(VideoId videoId, PlayerResponse response, CancellationToken ct)
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

            return WriteSynthesizedHls(videoId, videoStream, audioStream, response.DurationSeconds, videoSidx, audioSidx);
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
        ///     Parses the pre-fetched sidx boxes, builds the 3 playlists and writes them to the
        ///     temp cache, returning the absolute path of the master playlist. Returns null if
        ///     any step fails. Synchronous on purpose — see <see cref="TryWriteSynthesizedHlsAsync"/>.
        /// </summary>
        private static string? WriteSynthesizedHls(
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

                HlsManifestBuilder.PlaylistSet playlists =
                    HlsManifestBuilder.Build(videoStream, audioStream, durationSeconds, videoSegments, audioSegments);

                // Per-video subdirectory keeps the 3 files together so the master playlist's
                // relative URIs (audio.m3u8, video.m3u8) resolve correctly. Unity's
                // temporaryCachePath is OS-cleaned so we don't need to garbage-collect.
                string playlistDir = Path.Combine(Application.temporaryCachePath, SYNTH_HLS_DIR_PREFIX + videoId.Value);
                Directory.CreateDirectory(playlistDir);

                File.WriteAllText(Path.Combine(playlistDir, VIDEO_PLAYLIST_NAME), playlists.Video, HLS_ENCODING);
                File.WriteAllText(Path.Combine(playlistDir, AUDIO_PLAYLIST_NAME), playlists.Audio, HLS_ENCODING);

                string masterPath = Path.Combine(playlistDir, MASTER_PLAYLIST_NAME);
                File.WriteAllText(masterPath, playlists.Master, HLS_ENCODING);

                return masterPath;
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
