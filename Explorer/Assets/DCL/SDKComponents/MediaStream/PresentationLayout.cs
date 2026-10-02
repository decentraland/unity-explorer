using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Pure geometry over the presentation bot's protocol-v2 metadata. Rectangles are normalized to the slide size
    ///     as <c>(left, top, width, height)</c> with a top-left origin.
    /// </summary>
    public static class PresentationLayout
    {
        public const int MAX_SLIDE_SIZE = 4096;
        public const int MAX_SLIDE_URL_LENGTH = 2048;

        private const double MARGIN_RATIO = 0.02;
        private const double SMALL_CAMERA_RATIO = 0.15;
        private const double LARGE_CAMERA_RATIO = 0.25;
        private const string LARGE_CAMERA_SIZE = "large";

        private static readonly Regex PRESENTER_IDENTITY = new (@"^(stream:\S{1,121}|0x[0-9a-fA-F]{40})\z", RegexOptions.Compiled);

        /// <summary>
        ///     Deserializes bot metadata. Never throws and never logs.
        /// </summary>
        /// <returns>
        ///     <c>null</c> for empty or malformed input, or for a <c>slide</c> without a url, with a url longer than
        ///     <c>MAX_SLIDE_URL_LENGTH</c> or with a size outside <c>[1, MAX_SLIDE_SIZE]</c>. An invalid
        ///     <c>presenterIdentity</c> and an <c>overlay</c> with non-finite coordinates are dropped to <c>null</c>.
        /// </returns>
        public static PresentationBotMetadata? Parse(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            PresentationBotMetadata? metadata;

            try { metadata = JsonConvert.DeserializeObject<PresentationBotMetadata>(json); }
            catch (Exception) { return null; }

            if (metadata == null)
                return null;

            if (metadata.presenterIdentity != null && !PRESENTER_IDENTITY.IsMatch(metadata.presenterIdentity))
                metadata.presenterIdentity = null;

            if (metadata.overlay != null && (!double.IsFinite(metadata.overlay.x) || !double.IsFinite(metadata.overlay.y)))
                metadata.overlay = null;

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
