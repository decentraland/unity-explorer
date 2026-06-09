using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DCL.SDKComponents.MediaStream.YouTube
{
    /// <summary>
    ///     Synthesizes an HLS multivariant playlist locally from YouTube's adaptive video + audio
    ///     streams. Used as a last-resort A/V-sync fix for VOD videos that don't expose a native
    ///     <c>hlsManifestUrl</c> or <c>dashManifestUrl</c> — typically embed-restricted music
    ///     videos served only as legacy muxed MP4 (itag=18, ~360p, with known A/V drift).
    ///
    ///     HLS chosen over DASH because every AVPro backend supports it: AVFoundation (macOS/iOS),
    ///     Media Foundation (Windows), ExoPlayer (Android). DASH only works on a subset.
    ///
    ///     Output: three plain-text playlists meant to be written into the same directory.
    ///     - <c>master.m3u8</c> — multivariant playlist with one video stream + one audio rendition
    ///     - <c>video.m3u8</c> — single-segment fMP4 playlist using EXT-X-MAP + EXT-X-BYTERANGE
    ///     - <c>audio.m3u8</c> — same for audio
    ///
    ///     Reference: RFC 8216 §4.3 (HLS playlist tags), §4.3.2.5 (EXT-X-MAP byte-range form).
    /// </summary>
    internal static class HlsManifestBuilder
    {
        private const int DEFAULT_PLAYLIST_LENGTH = 2048;
        private const int HEADER_PLAYLIST_LENGTH = 256;
        private const int SEGMENT_PLAYLIST_LENGTH = 128;

        // Codecs every AVPro backend decodes reliably across Windows/macOS/iOS/Android.
        private const string PREFERRED_VIDEO_CODEC_PREFIX = "avc1";
        private const string PREFERRED_AUDIO_CODEC_PREFIX = "mp4a";
        private const int PREFERRED_HEIGHT = 1080;

        /// <summary>The 3 playlist contents, ready to write to disk in the same directory.</summary>
        public readonly struct PlaylistSet
        {
            public string Master { get; }
            public string Video { get; }
            public string Audio { get; }

            public PlaylistSet(string master, string video, string audio)
            {
                Master = master;
                Video = video;
                Audio = audio;
            }
        }

        /// <summary>
        ///     Selects the best video + audio pair from a list of adaptive formats and validates
        ///     them as synthesizable (byte ranges present, content length known). Useful when the
        ///     caller needs to know which streams will be used before invoking <see cref="Build(IReadOnlyList&lt;AdaptiveFormatData&gt;,int,IReadOnlyList&lt;SidxParser.SegmentInfo&gt;,IReadOnlyList&lt;SidxParser.SegmentInfo&gt;)"/>
        ///     — for example, to pre-fetch each stream's sidx box.
        /// </summary>
        public static bool TrySelectVideoAndAudio(
            IReadOnlyList<AdaptiveFormatData> adaptive,
            out AdaptiveFormatData video,
            out AdaptiveFormatData audio)
        {
            video = default;
            audio = default;

            if (adaptive == null || adaptive.Count == 0) return false;

            AdaptiveFormatData? v = SelectBestVideo(adaptive);
            AdaptiveFormatData? a = SelectBestAudio(adaptive);

            if (v == null || a == null) return false;
            if (!v.Value.HasByteRanges || v.Value.ContentLength <= 0) return false;
            if (!a.Value.HasByteRanges || a.Value.ContentLength <= 0) return false;

            video = v.Value;
            audio = a.Value;
            return true;
        }

        /// <summary>
        ///     Builds the 3 HLS playlists (master + video + audio) from the pre-selected pair
        ///     returned by <see cref="TrySelectVideoAndAudio"/>. If SIDX-derived segment tables
        ///     are supplied the media playlists are split into one HLS segment per fmp4
        ///     fragment — this avoids the multi-second buffer-fill stall AVPro exhibits when
        ///     handed a single byte range covering the entire video body (issue #8350). Falls
        ///     back to single-segment if either segment list is null or empty.
        ///
        ///     Caller is expected to have already validated the streams via
        ///     <see cref="TrySelectVideoAndAudio"/>; this method does not re-check byte ranges
        ///     or content length so the sidx data is structurally bound to the same streams
        ///     that get written into the playlists.
        /// </summary>
        public static PlaylistSet Build(
            AdaptiveFormatData video,
            AdaptiveFormatData audio,
            int durationSeconds,
            IReadOnlyList<SidxParser.SegmentInfo>? videoSegments = null,
            IReadOnlyList<SidxParser.SegmentInfo>? audioSegments = null)
        {
            bool segmented = videoSegments is { Count: > 0 }
                             && audioSegments is { Count: > 0 };

            string videoPlaylist = segmented
                ? BuildSegmentedMediaPlaylist(video, videoSegments!)
                : BuildMediaPlaylist(video, durationSeconds);

            string audioPlaylist = segmented
                ? BuildSegmentedMediaPlaylist(audio, audioSegments!)
                : BuildMediaPlaylist(audio, durationSeconds);

            return new PlaylistSet(
                BuildMaster(video, audio),
                videoPlaylist,
                audioPlaylist);
        }

        private static AdaptiveFormatData? SelectBestVideo(IReadOnlyList<AdaptiveFormatData> adaptive)
        {
            AdaptiveFormatData? best = null;
            long bestScore = -1;

            for (int i = 0; i < adaptive.Count; i++)
            {
                AdaptiveFormatData entry = adaptive[i];
                if (!entry.IsVideo) continue;
                if (!entry.HasByteRanges || entry.ContentLength <= 0) continue;

                string codec = ExtractCodec(entry.MimeType);
                if (!codec.StartsWith(PREFERRED_VIDEO_CODEC_PREFIX, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Prefer highest resolution up to PREFERRED_HEIGHT, then highest bitrate.
                // Resolutions above PREFERRED_HEIGHT score 0 so they only win as a fallback.
                int height = entry.Height!.Value <= PREFERRED_HEIGHT ? entry.Height.Value : 0;
                long score = ((long)height * 100_000_000L) + Math.Min(entry.Bitrate, 100_000_000L);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = entry;
                }
            }

            return best;
        }

        private static AdaptiveFormatData? SelectBestAudio(IReadOnlyList<AdaptiveFormatData> adaptive)
        {
            AdaptiveFormatData? best = null;
            long bestBitrate = -1;

            for (int i = 0; i < adaptive.Count; i++)
            {
                AdaptiveFormatData entry = adaptive[i];
                if (!entry.IsAudio) continue;
                if (entry.IsVideo) continue; // skip muxed/video entries that happen to have audio info
                if (!entry.HasByteRanges || entry.ContentLength <= 0) continue;

                string codec = ExtractCodec(entry.MimeType);
                if (!codec.StartsWith(PREFERRED_AUDIO_CODEC_PREFIX, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (entry.Bitrate > bestBitrate)
                {
                    bestBitrate = entry.Bitrate;
                    best = entry;
                }
            }

            return best;
        }

        // Pulls the codec string out of a YouTube mimeType like
        // <c>video/mp4; codecs="avc1.640028"</c> → <c>avc1.640028</c>.
        private static string ExtractCodec(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType)) return string.Empty;

            int codecsIdx = mimeType.IndexOf("codecs=", StringComparison.OrdinalIgnoreCase);
            if (codecsIdx < 0) return string.Empty;

            int start = mimeType.IndexOf('"', codecsIdx);
            if (start < 0) return string.Empty;

            int end = mimeType.IndexOf('"', start + 1);
            if (end < 0) return string.Empty;

            return mimeType.Substring(start + 1, end - start - 1);
        }

        // For multi-codec specs ("avc1.640028, mp4a.40.2") returns just the first one.
        // Adaptive formats always carry a single codec, but be defensive.
        private static string FirstCodec(string codecs)
        {
            int comma = codecs.IndexOf(',');
            return comma < 0 ? codecs : codecs.Substring(0, comma).Trim();
        }

        private static string BuildMaster(AdaptiveFormatData video, AdaptiveFormatData audio)
        {
            string videoCodec = FirstCodec(ExtractCodec(video.MimeType));
            string audioCodec = FirstCodec(ExtractCodec(audio.MimeType));
            long combinedBandwidth = video.Bitrate + audio.Bitrate;

            var sb = new StringBuilder(512);
            sb.Append("#EXTM3U\n");
            sb.Append("#EXT-X-VERSION:7\n");
            sb.Append("#EXT-X-INDEPENDENT-SEGMENTS\n");
            sb.Append('\n');
            sb.Append("#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio0\",NAME=\"audio\",DEFAULT=YES,AUTOSELECT=YES,URI=\"audio.m3u8\"\n");
            sb.Append('\n');
            sb.Append("#EXT-X-STREAM-INF:BANDWIDTH=").Append(combinedBandwidth);
            sb.Append(",CODECS=\"").Append(videoCodec).Append(',').Append(audioCodec).Append('"');
            sb.Append(",RESOLUTION=").Append(video.Width!.Value).Append('x').Append(video.Height!.Value);
            if (video.Fps.HasValue) sb.Append(",FRAME-RATE=").Append(video.Fps.Value);
            sb.Append(",AUDIO=\"audio0\"\n");
            sb.Append("video.m3u8\n");
            return sb.ToString();
        }

        private static string BuildSegmentedMediaPlaylist(AdaptiveFormatData stream, IReadOnlyList<SidxParser.SegmentInfo> segments)
        {
            // Init segment range: bytes [InitRangeStart..InitRangeEnd] inclusive.
            long initSize = stream.InitRangeEnd - stream.InitRangeStart + 1;
            long initOffset = stream.InitRangeStart;

            // Per-segment EXTINF needs to be a finite duration; ceil over the max sub-segment
            // duration for TARGETDURATION (HLS spec requires TARGETDURATION >= ceil(max EXTINF)).
            double maxSegmentDuration = 0;

            for (int i = 0; i < segments.Count; i++)
                if (segments[i].DurationSeconds > maxSegmentDuration)
                    maxSegmentDuration = segments[i].DurationSeconds;

            int targetDurationSeconds = Math.Max(1, (int)Math.Ceiling(maxSegmentDuration));

            // Pre-size: header (~200) + per-segment (~120) gets close enough.
            var sb = new StringBuilder(HEADER_PLAYLIST_LENGTH + (segments.Count * SEGMENT_PLAYLIST_LENGTH));
            sb.Append("#EXTM3U\n");
            sb.Append("#EXT-X-VERSION:7\n");
            sb.Append("#EXT-X-PLAYLIST-TYPE:VOD\n");
            sb.Append("#EXT-X-TARGETDURATION:").Append(targetDurationSeconds).Append('\n');
            sb.Append("#EXT-X-MEDIA-SEQUENCE:0\n");

            // EXT-X-MAP: the init segment (moov box) — shared across all segments.
            sb.Append("#EXT-X-MAP:URI=\"").Append(stream.Url).Append('"');
            sb.Append(",BYTERANGE=\"").Append(initSize).Append('@').Append(initOffset).Append("\"\n");

            // One HLS segment per fmp4 fragment. Each EXT-X-BYTERANGE points at a clean
            // fragment boundary (sidx-described), so AVPro can decode a single segment in
            // isolation and start playback after the first chunk lands.
            for (int i = 0; i < segments.Count; i++)
            {
                SidxParser.SegmentInfo seg = segments[i];
                sb.Append("#EXTINF:")
                    .Append(seg.DurationSeconds.ToString("0.######", CultureInfo.InvariantCulture))
                    .Append(",\n");
                sb.Append("#EXT-X-BYTERANGE:").Append(seg.ByteSize).Append('@').Append(seg.ByteOffset).Append('\n');
                sb.Append(stream.Url).Append('\n');
            }

            sb.Append("#EXT-X-ENDLIST\n");
            return sb.ToString();
        }

        private static string BuildMediaPlaylist(AdaptiveFormatData stream, int durationSeconds)
        {
            // Init segment range: bytes [InitRangeStart..InitRangeEnd] inclusive.
            // HLS BYTERANGE format is "<size>@<offset>".
            long initSize = stream.InitRangeEnd - stream.InitRangeStart + 1;
            long initOffset = stream.InitRangeStart;

            // Media data starts right after the index segment ends, runs to end of file.
            long mediaOffset = stream.IndexRangeEnd + 1;
            long mediaSize = stream.ContentLength - mediaOffset;

            var sb = new StringBuilder(DEFAULT_PLAYLIST_LENGTH);
            sb.Append("#EXTM3U\n");
            sb.Append("#EXT-X-VERSION:7\n");
            sb.Append("#EXT-X-PLAYLIST-TYPE:VOD\n");
            sb.Append("#EXT-X-TARGETDURATION:").Append(durationSeconds).Append('\n');
            sb.Append("#EXT-X-MEDIA-SEQUENCE:0\n");

            // EXT-X-MAP: the init segment (moov box). URI plus byte range within that URI.
            sb.Append("#EXT-X-MAP:URI=\"").Append(stream.Url).Append('"');
            sb.Append(",BYTERANGE=\"").Append(initSize).Append('@').Append(initOffset).Append("\"\n");

            // Single segment for the entire media payload. HLS doesn't require fine-grained
            // segments — one big segment is legal and avoids needing to parse the SIDX box to
            // discover sub-segment offsets. The segment URL repeats the same googlevideo URL.
            sb.Append("#EXTINF:").Append(durationSeconds).Append(".0,\n");
            sb.Append("#EXT-X-BYTERANGE:").Append(mediaSize).Append('@').Append(mediaOffset).Append('\n');
            sb.Append(stream.Url).Append('\n');
            sb.Append("#EXT-X-ENDLIST\n");
            return sb.ToString();
        }
    }
}
