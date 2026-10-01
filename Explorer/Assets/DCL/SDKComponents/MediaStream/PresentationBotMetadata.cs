using System;

// ReSharper disable InconsistentNaming
namespace DCL.SDKComponents.MediaStream
{
    // Wire source: decentraland/cast-presenter-server src/adapters/livekit-publisher/component.ts updateMetadataState ({ role, presentationId, ...PresentationState } + client-composition fields); all optional because the bot's initial metadata is only { role, presentationId } and the explorer treats it as untrusted input
    [Serializable]
    public class PresentationBotMetadata
    {
        public PresentationSlide? slide;
        public string? presenterIdentity;
#pragma warning disable UAC1001
        public int? playingVideoIndex;
#pragma warning restore UAC1001
        public string? videoState;
        public PresentationSlideVideo?[]? slideVideos;
        public PresentationOverlay? overlay;
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
