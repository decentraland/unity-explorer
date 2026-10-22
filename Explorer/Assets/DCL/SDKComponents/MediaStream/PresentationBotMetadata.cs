using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;
using UnityEngine;

// ReSharper disable InconsistentNaming
namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Server schema: decentraland/cast-presenter-server src/logic/presentation-manager/component.ts#/v2Fields
    ///     (published as bot participant metadata { role, presentationId, ...state }). Every field is optional:
    ///     the bot's initial metadata is only { role, presentationId }, and the explorer treats it as untrusted input.
    /// </summary>
    [Serializable]
    public partial class PresentationBotMetadata
    {
        public PresentationSlide? slide;
        public string? presenterIdentity;
#pragma warning disable UAC1001
        public int? playingVideoIndex;
        public long? videoPositionMs;
        public long? videoPositionAt;
#pragma warning restore UAC1001
        public PresentationSlideVideo?[]? slideVideos;
        public PresentationOverlay? overlay;
        public string? videoState;
    }

    /// <summary>
    ///     Parsing, pure geometry and playback sync over the bot's protocol-v2 metadata. Rectangles are normalized to the slide size
    ///     as <c>(left, top, width, height)</c> with a top-left origin.
    /// </summary>
    public partial class PresentationBotMetadata
    {
        public const int MAX_SLIDE_SIZE = LiveKitMediaExtensions.MAX_LIVEKIT_TEXTURE_SIZE;
        public const int MAX_SLIDE_URL_LENGTH = 2048;

        private const double MARGIN_RATIO = 0.02;
        private const double SMALL_CAMERA_RATIO = 0.15;
        private const double LARGE_CAMERA_RATIO = 0.25;
        private const string LARGE_CAMERA_SIZE = "large";
        private const string PLAYING_STATE = "playing";
        private const string PAUSED_STATE = "paused";
        private const string CLIENT_SOURCE = "client";
        private const long MAX_VIDEO_POSITION_MS = 100_000_000_000_000;
        private const double MS_PER_SECOND = 1000.0;

        private static readonly Regex PRESENTER_IDENTITY = new (@"^(stream:\S{1,121}|0x[0-9a-fA-F]{40})\z", RegexOptions.Compiled);

        private static readonly Regex CLIENT_VIDEO_URL = new (
            @"^(https://www\.youtube\.com/watch\?v=[A-Za-z0-9_-]{11}|https://drive\.google\.com/file/d/[A-Za-z0-9_-]{10,128}/view|https://player\.vimeo\.com/external/[0-9]+\.m3u8(\?[!-""$-~]{1,512})?)\z",
            RegexOptions.Compiled);

        /// <summary>
        ///     Deserializes bot metadata. Never throws and never logs.
        /// </summary>
        /// <returns>
        ///     <c>null</c> for empty or malformed input, or for a <c>slide</c> without a url, with a url longer than
        ///     <c>MAX_SLIDE_URL_LENGTH</c> or with a size outside <c>[1, MAX_SLIDE_SIZE]</c>. An invalid
        ///     <c>presenterIdentity</c>, an <c>overlay</c> with non-finite coordinates and a <c>videoPositionMs</c> or
        ///     <c>videoPositionAt</c> outside <c>[0, MAX_VIDEO_POSITION_MS]</c> are dropped to <c>null</c>.
        /// </returns>
        public static PresentationBotMetadata? Parse(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            PresentationBotMetadata? metadata;

            try { metadata = JsonConvert.DeserializeObject<PresentationBotMetadata>(json); }
            catch (JsonException) { return null; }

            if (metadata == null)
                return null;

            if (metadata.presenterIdentity != null && !PRESENTER_IDENTITY.IsMatch(metadata.presenterIdentity))
                metadata.presenterIdentity = null;

            if (metadata.overlay != null && (!double.IsFinite(metadata.overlay.x) || !double.IsFinite(metadata.overlay.y)))
                metadata.overlay = null;

            if (metadata.videoPositionMs is < 0 or > MAX_VIDEO_POSITION_MS)
                metadata.videoPositionMs = null;

            if (metadata.videoPositionAt is < 0 or > MAX_VIDEO_POSITION_MS)
                metadata.videoPositionAt = null;

            PresentationSlide? parsedSlide = metadata.slide;

            if (parsedSlide == null)
                return metadata;

            return string.IsNullOrEmpty(parsedSlide.url) || parsedSlide.url.Length > MAX_SLIDE_URL_LENGTH || !IsValidSlideSize(parsedSlide.width) || !IsValidSlideSize(parsedSlide.height) ? null : metadata;
        }

        /// <summary>
        ///     The rectangle of the video at <c>playingVideoIndex</c>, regardless of <c>videoState</c>.
        /// </summary>
        /// <returns><c>false</c> when there is no slide, no valid index, or no positive-size geometry for that entry.</returns>
        public bool TryVideoRect(out Vector4 rect)
        {
            rect = default;
            PresentationSlide? currentSlide = slide;

            if (currentSlide == null)
                return false;

            PresentationRect? geometry = PlayingVideo()?.geometry;

            if (geometry == null || !float.IsFinite(geometry.x) || !float.IsFinite(geometry.y) || !float.IsFinite(geometry.width) || !float.IsFinite(geometry.height)
                || !(geometry.width > 0) || !(geometry.height > 0))
                return false;

            rect = new Vector4(geometry.x / currentSlide.width, geometry.y / currentSlide.height, geometry.width / currentSlide.width, geometry.height / currentSlide.height);
            return true;
        }

        /// <summary>
        ///     The canonical url of the client-played video at <c>playingVideoIndex</c>.
        /// </summary>
        /// <returns>
        ///     <c>false</c>, with an empty <paramref name="url" />, unless <c>videoState</c> is playing or paused and the entry's
        ///     <c>source</c> is <c>client</c> with a canonical YouTube watch, Google Drive view or Vimeo external m3u8 url. A Vimeo query is
        ///     at most 512 printable ASCII characters without <c>#</c>.
        /// </returns>
        public bool TryClientVideoUrl(out string url)
        {
            url = string.Empty;

            if (!IsPlayingOrPaused(videoState) || PlayingVideo() is not { source: CLIENT_SOURCE, url: { } candidate } || !CLIENT_VIDEO_URL.IsMatch(candidate))
                return false;

            url = candidate;
            return true;
        }

        /// <summary>
        ///     The presenter's playback position: the anchor plus the time elapsed since it while playing, the anchor itself while paused.
        /// </summary>
        /// <returns>
        ///     <c>null</c> unless <c>videoState</c> is playing or paused and both position fields are present, and while playing with an anchor
        ///     later than <paramref name="nowEpochMs" />, whose clock skew is unknown; never negative.
        /// </returns>
        public double? TargetPositionSeconds(long nowEpochMs)
        {
            if (videoPositionMs is not { } positionMs || videoPositionAt is not { } positionAt)
                return null;

            return videoState switch
                   {
                       PLAYING_STATE when nowEpochMs >= positionAt => Math.Max(0, positionMs + nowEpochMs - positionAt) / MS_PER_SECOND,
                       PLAYING_STATE => null,
                       PAUSED_STATE => Math.Max(0, positionMs) / MS_PER_SECOND,
                       _ => null,
                   };
        }

        /// <summary>
        ///     Whether <c>videoState</c> is playing; paused, loading and idle are not.
        /// </summary>
        public bool IsVideoPlaying() =>
            videoState == PLAYING_STATE;

        /// <summary>
        ///     Whether the <c>slideVideos</c> entry at <c>playingVideoIndex</c> has <c>source</c> <c>client</c>, whatever <c>videoState</c> is.
        /// </summary>
        public bool IsClientEntry() =>
            PlayingVideo()?.source == CLIENT_SOURCE;

        /// <summary>
        ///     The presenter camera circle's bounding square, computed in integer pixels exactly as the server does.
        ///     A null <c>overlay</c> means bottom-left small; an unknown size means small.
        /// </summary>
        /// <returns><see cref="Vector4.zero" /> when the diameter is below two pixels.</returns>
        public Vector4 CameraRect(int slideWidth, int slideHeight)
        {
            double ratio = overlay?.size == LARGE_CAMERA_SIZE ? LARGE_CAMERA_RATIO : SMALL_CAMERA_RATIO;
            int margin = Round(slideWidth * MARGIN_RATIO);
            int diameter = Even(Math.Min(Round(slideWidth * ratio), slideHeight - (2 * margin)));

            if (diameter < 2)
                return Vector4.zero;

            double radius = diameter / 2.0;
            double centerX = Clamp((overlay?.x ?? 0) * slideWidth, margin + radius, slideWidth - margin - radius);
            double centerY = Clamp((overlay?.y ?? 1) * slideHeight, margin + radius, slideHeight - margin - radius);
            int left = Even(Round(centerX - radius));
            int top = Even(Round(centerY - radius));

            return new Vector4((float)left / slideWidth, (float)top / slideHeight, (float)diameter / slideWidth, (float)diameter / slideHeight);
        }

        private PresentationSlideVideo? PlayingVideo() =>
            slideVideos is { } videos && playingVideoIndex is { } index && index >= 0 && index < videos.Length ? videos[index] : null;

        private static bool IsPlayingOrPaused(string? state) =>
            state is PLAYING_STATE or PAUSED_STATE;

        private static bool IsValidSlideSize(int size) =>
            size is >= 1 and <= MAX_SLIDE_SIZE;

        private static int Round(double value) =>
            (int)Math.Floor(value + 0.5);

        private static int Even(int value) =>
            value - (value % 2);

        private static double Clamp(double value, double min, double max) =>
            Math.Min(Math.Max(value, min), max);
    }

    [Serializable]
    public class PresentationSlide
    {
        public string? url;
        public int width;
        public int height;
    }

    [Serializable]
    public class PresentationSlideVideo
    {
        public PresentationRect? geometry;
        public string? url;
        public string? source;
    }

    [Serializable]
    public class PresentationRect
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public class PresentationOverlay
    {
        public double x;
        public double y;
        public string? size;
    }
}
