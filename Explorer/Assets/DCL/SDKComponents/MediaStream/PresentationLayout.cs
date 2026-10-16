using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Pure geometry and playback sync over the presentation bot's protocol-v2 metadata. Rectangles are normalized to the slide size
    ///     as <c>(left, top, width, height)</c> with a top-left origin.
    /// </summary>
    public static class PresentationLayout
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
        private const double SEEK_THRESHOLD_SECONDS = 2.0;

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
        ///     <c>presenterIdentity</c>, an <c>overlay</c> with non-finite coordinates and a negative
        ///     <c>videoPositionMs</c> or <c>videoPositionAt</c> are dropped to <c>null</c>.
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

            if (metadata.videoPositionMs < 0)
                metadata.videoPositionMs = null;

            if (metadata.videoPositionAt < 0)
                metadata.videoPositionAt = null;

            PresentationSlide? slide = metadata.slide;

            if (slide == null)
                return metadata;

            return string.IsNullOrEmpty(slide.url) || slide.url.Length > MAX_SLIDE_URL_LENGTH || !IsValidSlideSize(slide.width) || !IsValidSlideSize(slide.height) ? null : metadata;
        }

        /// <summary>
        ///     The rectangle of the video at <c>playingVideoIndex</c>, regardless of <c>videoState</c>.
        /// </summary>
        /// <returns><c>false</c> when there is no slide, no valid index, or no positive-size geometry for that entry.</returns>
        public static bool TryVideoRect(PresentationBotMetadata metadata, out Vector4 rect)
        {
            rect = default;
            PresentationSlide? slide = metadata.slide;
            PresentationSlideVideo?[]? videos = metadata.slideVideos;

            if (slide == null || videos == null || metadata.playingVideoIndex is not { } index || index < 0 || index >= videos.Length)
                return false;

            PresentationRect? geometry = videos[index]?.geometry;

            if (geometry == null || !float.IsFinite(geometry.x) || !float.IsFinite(geometry.y) || !float.IsFinite(geometry.width) || !float.IsFinite(geometry.height)
                || !(geometry.width > 0) || !(geometry.height > 0))
                return false;

            rect = new Vector4(geometry.x / slide.width, geometry.y / slide.height, geometry.width / slide.width, geometry.height / slide.height);
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
        public static bool TryClientVideoUrl(PresentationBotMetadata metadata, out string url)
        {
            url = string.Empty;
            PresentationSlideVideo?[]? videos = metadata.slideVideos;

            if (!IsPlayingOrPaused(metadata.videoState) || videos == null || metadata.playingVideoIndex is not { } index || index < 0 || index >= videos.Length)
                return false;

            if (videos[index] is not { source: CLIENT_SOURCE, url: { } candidate } || !CLIENT_VIDEO_URL.IsMatch(candidate))
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
        public static double? TargetPositionSeconds(PresentationBotMetadata metadata, long nowEpochMs)
        {
            if (metadata.videoPositionMs is not { } positionMs || metadata.videoPositionAt is not { } positionAt)
                return null;

            return metadata.videoState switch
                   {
                       PLAYING_STATE when nowEpochMs >= positionAt => Math.Max(0, positionMs + nowEpochMs - positionAt) / 1000.0,
                       PLAYING_STATE => null,
                       PAUSED_STATE => Math.Max(0, positionMs) / 1000.0,
                       _ => null,
                   };
        }

        /// <summary>
        ///     Whether the player has drifted more than two seconds from <paramref name="targetSeconds" />. A live stream never needs a seek.
        /// </summary>
        public static bool NeedsSeek(double currentSeconds, double? targetSeconds, bool isLive) =>
            !isLive && targetSeconds is { } target && Math.Abs(currentSeconds - target) > SEEK_THRESHOLD_SECONDS;

        /// <summary>
        ///     Whether <paramref name="targetSeconds" /> is more than two seconds behind <paramref name="currentSeconds" />. A live stream never rewinds.
        /// </summary>
        public static bool IsRewind(double currentSeconds, double? targetSeconds, bool isLive) =>
            targetSeconds < currentSeconds && NeedsSeek(currentSeconds, targetSeconds, isLive);

        public static bool ShouldSeek(double currentSeconds, double? targetSeconds, bool isLive, bool isBusy, float now, float lastSeekAt) =>
            false;

        public static bool IsVideoPlaying(PresentationBotMetadata metadata) =>
            metadata.videoState == PLAYING_STATE;

        /// <summary>
        ///     Whether the <c>slideVideos</c> entry at <c>playingVideoIndex</c> has <c>source</c> <c>client</c>, whatever <c>videoState</c> is.
        /// </summary>
        public static bool IsClientEntry(PresentationBotMetadata metadata) =>
            metadata.slideVideos is { } videos && metadata.playingVideoIndex is { } index && index >= 0 && index < videos.Length && videos[index]?.source == CLIENT_SOURCE;

        /// <summary>
        ///     The presenter camera circle's bounding square, computed in integer pixels exactly as the server does.
        ///     A null overlay means bottom-left small; an unknown size means small.
        /// </summary>
        /// <returns><see cref="Vector4.zero" /> when the diameter is below two pixels.</returns>
        public static Vector4 CameraRect(PresentationOverlay? overlay, int slideWidth, int slideHeight)
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

        private static bool IsPlayingOrPaused(string? videoState) =>
            videoState is PLAYING_STATE or PAUSED_STATE;

        private static bool IsValidSlideSize(int size) =>
            size is >= 1 and <= MAX_SLIDE_SIZE;

        private static int Round(double value) =>
            (int)Math.Floor(value + 0.5);

        private static int Even(int value) =>
            value - (value % 2);

        private static double Clamp(double value, double min, double max) =>
            Math.Min(Math.Max(value, min), max);
    }
}
