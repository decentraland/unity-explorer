using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;
using UnityEngine;

// ReSharper disable InconsistentNaming
namespace DCL.SDKComponents.MediaStream
{
    // Server schema: decentraland/cast-presenter-server src/logic/presentation-manager/component.ts#/v2Fields (published as bot participant metadata { role, presentationId, ...state })
    // (every field is optional: the bot's initial metadata is only { role, presentationId }, and the explorer treats it as untrusted input)
    [Serializable]
    public partial class PresentationBotMetadata
    {
        public PresentationSlide? slide;
        public string? presenterIdentity;
#pragma warning disable UAC1001
        public int? playingVideoIndex;
#pragma warning restore UAC1001
        public PresentationSlideVideo?[]? slideVideos;
        public PresentationOverlay? overlay;
    }

    /// <summary>
    ///     Parsing and pure geometry over the bot's protocol-v2 metadata. Rectangles are normalized to the slide size
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
            catch (JsonException) { return null; }

            if (metadata == null)
                return null;

            if (metadata.presenterIdentity != null && !PRESENTER_IDENTITY.IsMatch(metadata.presenterIdentity))
                metadata.presenterIdentity = null;

            if (metadata.overlay != null && (!double.IsFinite(metadata.overlay.x) || !double.IsFinite(metadata.overlay.y)))
                metadata.overlay = null;

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
            PresentationSlideVideo?[]? videos = slideVideos;

            if (currentSlide == null || videos == null || playingVideoIndex is not { } index || index < 0 || index >= videos.Length)
                return false;

            PresentationRect? geometry = videos[index]?.geometry;

            if (geometry == null || !float.IsFinite(geometry.x) || !float.IsFinite(geometry.y) || !float.IsFinite(geometry.width) || !float.IsFinite(geometry.height)
                || !(geometry.width > 0) || !(geometry.height > 0))
                return false;

            rect = new Vector4(geometry.x / currentSlide.width, geometry.y / currentSlide.height, geometry.width / currentSlide.width, geometry.height / currentSlide.height);
            return true;
        }

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
