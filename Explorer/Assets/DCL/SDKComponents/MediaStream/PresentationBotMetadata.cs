using System;

// ReSharper disable InconsistentNaming
namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Server schema: decentraland/cast-presenter-server src/logic/presentation-manager/component.ts#/v2Fields
    ///     (published as bot participant metadata { role, presentationId, ...state }). Every field is optional:
    ///     the bot's initial metadata is only { role, presentationId }, and the explorer treats it as untrusted input.
    /// </summary>
    [Serializable]
    public class PresentationBotMetadata
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
