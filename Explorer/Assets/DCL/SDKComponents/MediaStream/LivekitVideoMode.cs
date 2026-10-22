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
    public readonly struct DeferredVideo : IEquatable<DeferredVideo>
    {
        public readonly LivekitAddress Requested;
        public readonly LivekitAddress Target;

        public DeferredVideo(LivekitAddress requested, LivekitAddress target)
        {
            Requested = requested;
            Target = target;
        }

        public bool Equals(DeferredVideo other) =>
            Requested.Equals(other.Requested) && Target.Equals(other.Target);

        public override bool Equals(object? obj) =>
            obj is DeferredVideo other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Requested, Target);
    }

    /// <summary>
    ///     Showing one LiveKit track chosen for <see cref="Target" />, which is the requested address or, after the
    ///     requested track went away, the current stream.
    /// </summary>
    public readonly struct LegacyVideo : IEquatable<LegacyVideo>
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

        public bool Equals(LegacyVideo other) =>
            Requested.Equals(other.Requested) && Target.Equals(other.Target) && Stream.Key.Equals(other.Stream.Key) && Stream.SwitchedAtTime.Equals(other.Stream.SwitchedAtTime);

        public override bool Equals(object? obj) =>
            obj is LegacyVideo other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Requested, Target, Stream.Key, Stream.SwitchedAtTime);
    }

    /// <summary>
    ///     Composing the presentation of the bot from its slide, its presentation video and the presenter
    ///     camera. Holds a <see cref="SlideTextureCache" /> composing lease until the player leaves this mode.
    /// </summary>
    public sealed class ComposingVideo
    {
        internal readonly LivekitAddress requested;
        internal readonly IDisposable slideLease;

        internal VideoStreamInfo? presentationVideo;
        internal VideoStreamInfo? presenterCamera;
        internal Texture2D? shownSlide;
        internal string? shownSlideUrl;
        internal Texture? composedTexture;
        internal int composedFrame = -1;

        internal string bot { get; private set; }

        internal PresentationBotMetadata presentation { get; private set; }

        internal PresentationSlide slide { get; private set; }

        internal ComposingVideo(LivekitAddress requested, IDisposable slideLease, string bot, PresentationBotMetadata presentation, PresentationSlide slide)
        {
            this.requested = requested;
            this.slideLease = slideLease;
            this.bot = bot;
            this.presentation = presentation;
            this.slide = slide;
        }

        /// <summary>
        ///     Switches to new metadata, keeping the held streams and the shown slide, and composes again on the next read.
        /// </summary>
        internal void Show(string nextBot, PresentationBotMetadata nextPresentation, PresentationSlide nextSlide)
        {
            bot = nextBot;
            presentation = nextPresentation;
            slide = nextSlide;
            composedFrame = -1;
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
            onComposingVideo: static composing => composing.requested
        );
    }
}
