using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SDKComponents.MediaStream.YouTube;
using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

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
                string? synthesizedPath = TryWriteSynthesizedHls(videoId, response);

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
        /// </summary>
        private static string? TryWriteSynthesizedHls(VideoId videoId, PlayerResponse response)
        {
            try
            {
                HlsManifestBuilder.PlaylistSet? playlists =
                    HlsManifestBuilder.Build(response.AdaptiveFormats, response.DurationSeconds);

                if (playlists == null)
                {
                    ReportHub.Log(ReportCategory.MEDIA_STREAM,
                        $"[{TAG}] HLS synthesis skipped for {videoId.Value} — no usable mp4 video+audio adaptive pair");
                    return null;
                }

                // Per-video subdirectory keeps the 3 files together so the master playlist's
                // relative URIs (audio.m3u8, video.m3u8) resolve correctly. Unity's
                // temporaryCachePath is OS-cleaned so we don't need to garbage-collect.
                string playlistDir = Path.Combine(Application.temporaryCachePath, SYNTH_HLS_DIR_PREFIX + videoId.Value);
                Directory.CreateDirectory(playlistDir);

                File.WriteAllText(Path.Combine(playlistDir, VIDEO_PLAYLIST_NAME), playlists.Value.Video, HLS_ENCODING);
                File.WriteAllText(Path.Combine(playlistDir, AUDIO_PLAYLIST_NAME), playlists.Value.Audio, HLS_ENCODING);

                string masterPath = Path.Combine(playlistDir, MASTER_PLAYLIST_NAME);
                File.WriteAllText(masterPath, playlists.Value.Master, HLS_ENCODING);

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
