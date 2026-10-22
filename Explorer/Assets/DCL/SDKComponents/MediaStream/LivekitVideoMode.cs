using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.VideoStreaming;
using REnum;
using RichTypes;
using System;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     A LiveKit video track held by a player, with the time it was switched to.
    /// </summary>
    public readonly struct VideoStreamInfo
    {
        public readonly StreamKey Key;
        public readonly Weak<IVideoStream> VideoStream;
        public readonly float SwitchedAtTime;

        public string FromIdentity => Key.identity;

        public bool IsLive => VideoStream.Resource.Has;

        private VideoStreamInfo(StreamKey key, Weak<IVideoStream> videoStream, float switchedAtTime)
        {
            Key = key;
            VideoStream = videoStream;
            SwitchedAtTime = switchedAtTime;
        }

        public static VideoStreamInfo New(StreamKey key, Weak<IVideoStream> videoStream) =>
            new (key, videoStream, UnityEngine.Time.realtimeSinceStartup);

        public bool IsFromPresentationBot() =>
            Key.identity.IsPresentationBotIdentity();
    }

    /// <summary>
    ///     Opened, but holding no track: <see cref="Target" /> is opened once the room is connected and a track matches it.
    /// </summary>
    public readonly struct DeferredVideo
    {
        public readonly LivekitAddress Requested;
        public readonly LivekitAddress Target;

        public DeferredVideo(LivekitAddress requested, LivekitAddress target)
        {
            Requested = requested;
            Target = target;
        }
    }

    /// <summary>
    ///     Showing one LiveKit track chosen for <see cref="Target" />, which is the requested address or, after the
    ///     requested track went away, the current stream.
    /// </summary>
    public readonly struct LegacyVideo
    {
        public readonly LivekitAddress Requested;
        public readonly LivekitAddress Target;
        public readonly VideoStreamInfo Stream;

        public LegacyVideo(LivekitAddress requested, LivekitAddress target, VideoStreamInfo stream)
        {
            Requested = requested;
            Target = target;
            Stream = stream;
        }
    }

    /// <summary>
    ///     Composing the presentation of <see cref="Bot" /> from its slide, its presentation video and the presenter
    ///     camera. Holds a <see cref="SlideTextureCache" /> composing lease until the player leaves this mode.
    /// </summary>
    public sealed class ComposingVideo
    {
        internal readonly LivekitAddress Requested;
        internal readonly IDisposable SlideLease;

        internal VideoStreamInfo? PresentationVideo;
        internal VideoStreamInfo? PresenterCamera;
        internal Texture2D? ShownSlide;
        internal string? ShownSlideUrl;
        internal Texture? ComposedTexture;
        internal int ComposedFrame = -1;

        internal string Bot { get; private set; }

        internal PresentationBotMetadata Presentation { get; private set; }

        internal PresentationSlide Slide { get; private set; }

        internal ComposingVideo(LivekitAddress requested, IDisposable slideLease, string bot, PresentationBotMetadata presentation, PresentationSlide slide)
        {
            Requested = requested;
            SlideLease = slideLease;
            Bot = bot;
            Presentation = presentation;
            Slide = slide;
        }

        /// <summary>
        ///     Switches to new metadata, keeping the held streams and the shown slide, and composes again on the next read.
        /// </summary>
        internal void Show(string bot, PresentationBotMetadata presentation, PresentationSlide slide)
        {
            Bot = bot;
            Presentation = presentation;
            Slide = slide;
            ComposedFrame = -1;
        }
    }

    /// <summary>
    ///     What a <see cref="LivekitPlayer" /> shows. <c>Closed</c> holds nothing; every other mode keeps the address the
    ///     player was opened with.
    /// </summary>
    [REnum]
    [REnumFieldEmpty("Closed")]
    [REnumField(typeof(DeferredVideo))]
    [REnumField(typeof(LegacyVideo))]
    [REnumField(typeof(ComposingVideo))]
    public partial struct LivekitVideoMode
    {
        /// <summary>
        ///     The address the player was opened with, or <c>null</c> when closed.
        /// </summary>
        public LivekitAddress? Requested => Match<LivekitAddress?>(
            onClosed: static () => null,
            onDeferredVideo: static deferred => deferred.Requested,
            onLegacyVideo: static legacy => legacy.Requested,
            onComposingVideo: static composing => composing.Requested
        );
    }
}
