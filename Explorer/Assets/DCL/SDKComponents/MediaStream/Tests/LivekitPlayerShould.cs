using DCL.LiveKit.Public;
using DCL.WebRequests;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using LiveKit.Rooms.Tracks.Hub;
using LiveKit.Rooms.VideoStreaming;
using NSubstitute;
using NUnit.Framework;
using RichTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.MediaStream.Tests
{
    [TestFixture]
    public class LivekitPlayerShould
    {
        private const string BOT = "presentation-bot:room:1";
        private const string PRESENTER = "stream:place:1";
        private const string OTHER = "0xOTHER";
        private const string STREAMER = "0xSTREAMER";
        private const string NEW_STREAMER = "0xNEWSTREAMER";
        private const string CAMERA_SID = "TR_camera";
        private const string NEW_CAMERA_SID = "TR_camera_2";
        private const string SCREEN_SID = "TR_screen";
        private const string SLIDE_URL = "https://example.com/slide.png";
        private const string LEGACY_METADATA = "{\"role\":\"presentation\",\"presentationId\":\"p1\"}";
        private const string V2_METADATA = "{\"role\":\"presentation\",\"slide\":{\"url\":\"" + SLIDE_URL + "\",\"width\":1920,\"height\":1080},\"presenterIdentity\":\"" + PRESENTER
                                           + "\",\"playingVideoIndex\":null,\"videoState\":\"idle\",\"slideVideos\":[],\"overlay\":{\"x\":0,\"y\":1,\"size\":\"small\"}}";
        private const string SLIDE_VIDEO = "{\"geometry\":{\"x\":480,\"y\":270,\"width\":960,\"height\":540}}";

        private static readonly Vector2 FLIPPED = new (1f, -1f);

        private IRoom room = null!;
        private IParticipantsHub participantsHub = null!;
        private IVideoStreams videoStreams = null!;
        private FakeActiveSpeakers activeSpeakers = null!;
        private Dictionary<string, LKParticipant> remoteParticipants = null!;
        private Dictionary<StreamKey, Weak<IVideoStream>> resolvedStreams = null!;
        private List<Object> created = null!;
        private List<SlideTextureCache> slideCaches = null!;
        private LivekitPlayer? player;

        [SetUp]
        public void SetUp()
        {
            room = Substitute.For<IRoom>();
            participantsHub = Substitute.For<IParticipantsHub>();
            videoStreams = Substitute.For<IVideoStreams>();
            activeSpeakers = new FakeActiveSpeakers();
            remoteParticipants = new Dictionary<string, LKParticipant>();
            resolvedStreams = new Dictionary<StreamKey, Weak<IVideoStream>>();
            created = new List<Object>();
            slideCaches = new List<SlideTextureCache>();
            player = null;

            room.Participants.Returns(participantsHub);
            room.VideoStreams.Returns(videoStreams);
            room.ActiveSpeakers.Returns(activeSpeakers);
            room.Info.ConnectionState.Returns(LKConnectionState.ConnConnected);
            participantsHub.RemoteParticipantIdentities().Returns(remoteParticipants);
            videoStreams.ActiveStream(Arg.Any<StreamKey>())
                        .Returns(ci => resolvedStreams.TryGetValue(ci.Arg<StreamKey>(), out Weak<IVideoStream> weak) ? weak : Weak<IVideoStream>.Null);
        }

        [TearDown]
        public void TearDown()
        {
            player?.Dispose();

            foreach (SlideTextureCache cache in slideCaches)
                cache.Dispose();

            foreach (Object obj in created)
                Object.DestroyImmediate(obj);
        }

        [Test]
        public void ComposePresentation_WhenBotMetadataHasSlide()
        {
            AddParticipant(BOT, V2_METADATA);
            LivekitPlayer p = NewV2Player();

            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            Assert.IsTrue(p.IsVideoOpened);
            Assert.AreEqual(Vector2.one, p.CurrentTextureScale);
            AssertComposite(p.LastTexture());
        }

        [Test]
        public void OpenPresentationVideoByName_WhenComposing()
        {
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LivekitPlayer p = NewV2Player();

            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            videoStreams.Received().ActiveStream(new StreamKey(BOT, "TR_pv"));
        }

        [Test]
        public void OpenPresenterCamera_WhenPresenterIdentityIsSet()
        {
            AddParticipant(BOT, V2_METADATA);
            LKParticipant presenter = AddParticipant(PRESENTER);
            Subscribe(presenter, AddTrack(presenter, "TR_cam", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewV2Player();

            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            videoStreams.Received().ActiveStream(new StreamKey(PRESENTER, "TR_cam"));
        }

        [Test]
        public void NotOpenOtherCameras_WhenComposing()
        {
            LKParticipant other = AddParticipant(OTHER);
            Subscribe(other, AddTrack(other, "TR_other", TrackKind.KindVideo, TrackSource.SourceCamera));
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LKParticipant presenter = AddParticipant(PRESENTER);
            Subscribe(presenter, AddTrack(presenter, "TR_cam", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewV2Player();

            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            p.EnsureVideoIsPlaying();

            videoStreams.DidNotReceive().ActiveStream(new StreamKey(OTHER, "TR_other"));
        }

        [Test]
        public void KeepLegacyTrack_WhenMetadataHasNoSlide()
        {
            LKParticipant bot = AddParticipant(BOT, LEGACY_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_p", TrackKind.KindVideo, TrackSource.SourceCamera, "presentation"));
            LivekitPlayer p = NewV2Player();

            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            videoStreams.Received().ActiveStream(new StreamKey(BOT, "TR_p"));
            Assert.IsTrue(p.IsVideoOpened);
            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);
        }

        [Test]
        public void NeverSelectPresentationVideo_OnLegacyPath()
        {
            LKParticipant bot = AddParticipant(BOT, LEGACY_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LKParticipant other = AddParticipant(OTHER);
            Subscribe(other, AddTrack(other, "TR_other", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewV2Player();

            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            videoStreams.Received().ActiveStream(new StreamKey(OTHER, "TR_other"));
            videoStreams.DidNotReceive().ActiveStream(new StreamKey(BOT, "TR_pv"));
        }

        [Test]
        public void NotCompose_WhenCompositorMaterialIsMissing()
        {
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_p", TrackKind.KindVideo, TrackSource.SourceCamera, "presentation"));
            LivekitPlayer p = NewLegacyPlayer();

            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            videoStreams.Received().ActiveStream(new StreamKey(BOT, "TR_p"));
            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);
        }

        [Test]
        public void SwitchToComposite_WhenMetadataChangesMidSession()
        {
            LKParticipant bot = AddParticipant(BOT, LEGACY_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_p", TrackKind.KindVideo, TrackSource.SourceCamera, "presentation"));
            LivekitPlayer p = NewV2Player();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);

            SetMetadata(bot, V2_METADATA);
            p.EnsureVideoIsPlaying();

            Assert.AreEqual(Vector2.one, p.CurrentTextureScale);
            AssertComposite(p.LastTexture());
        }

        [Test]
        public void FallBackToLegacy_WhenSlideDisappears()
        {
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_p", TrackKind.KindVideo, TrackSource.SourceCamera, "presentation"));
            LivekitPlayer p = NewV2Player();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            Assert.AreEqual(Vector2.one, p.CurrentTextureScale);
            videoStreams.DidNotReceive().ActiveStream(new StreamKey(BOT, "TR_p"));

            SetMetadata(bot, LEGACY_METADATA);
            p.EnsureVideoIsPlaying();

            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);
            Assert.IsTrue(p.IsVideoOpened);
            videoStreams.Received().ActiveStream(new StreamKey(BOT, "TR_p"));
        }

        [Test]
        public void ComposeForPinnedBotAddress()
        {
            AddParticipant(BOT, V2_METADATA);
            LivekitPlayer p = NewV2Player();

            p.OpenMedia(LivekitAddress.FromUserStream(new UserStream(BOT, "TR_old")));
            p.EnsureVideoIsPlaying();

            Assert.IsTrue(p.IsVideoOpened);
            Assert.AreEqual(Vector2.one, p.CurrentTextureScale);
            AssertComposite(p.LastTexture());
        }

        [Test]
        public void ComposeForPinnedPresentationVideoSid()
        {
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            IVideoStream video = Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            video.DecodeLastFrame().Returns((Texture2D?)null);
            LivekitPlayer p = NewV2Player();

            p.OpenMedia(LivekitAddress.FromUserStream(new UserStream(BOT, "TR_pv")));
            p.EnsureVideoIsPlaying();

            Assert.IsTrue(p.IsVideoOpened);
            Assert.AreEqual(Vector2.one, p.CurrentTextureScale);
            AssertComposite(p.LastTexture());
        }

        [Test]
        public void NotCompose_ForPinnedNonBotAddress()
        {
            LKParticipant other = AddParticipant(OTHER);
            Subscribe(other, AddTrack(other, "TR_other", TrackKind.KindVideo, TrackSource.SourceCamera));
            AddParticipant(BOT, V2_METADATA);
            LivekitPlayer p = NewV2Player();

            p.OpenMedia(LivekitAddress.FromUserStream(new UserStream(OTHER, "TR_other")));
            p.EnsureVideoIsPlaying();

            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);
            videoStreams.Received().ActiveStream(new StreamKey(OTHER, "TR_other"));
        }

        [Test]
        public void OpenPreSubscribedTracks_WhenPlayerCreatedAfterSubscription()
        {
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LKParticipant presenter = AddParticipant(PRESENTER);
            Subscribe(presenter, AddTrack(presenter, "TR_cam", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewV2Player();

            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            videoStreams.Received().ActiveStream(new StreamKey(BOT, "TR_pv"));
            videoStreams.Received().ActiveStream(new StreamKey(PRESENTER, "TR_cam"));
        }

        [Test]
        public void DrawVideo_WhenFreshFrameArrivesAfterIndexChange()
        {
            (LivekitPlayer p, _) = DrawSecondFrame();

            Assert.IsTrue(p.lastComposeDrewVideo);
        }

        [Test]
        public void KeepDrawingFrozenFrame_WhenPaused()
        {
            (LivekitPlayer p, LKParticipant bot) = DrawSecondFrame();

            SetMetadata(bot, V2("0", "paused", 1));
            p.EnsureVideoIsPlaying();
            p.LastTexture();

            Assert.IsTrue(p.lastComposeDrewVideo);
        }

        [Test]
        public void NotDrawPreviousVideoFrame_WhenNewVideoStarts()
        {
            (LivekitPlayer p, LKParticipant bot) = DrawSecondFrame();

            SetMetadata(bot, V2("1", "playing", 1));
            p.EnsureVideoIsPlaying();
            p.LastTexture();

            Assert.IsFalse(p.lastComposeDrewVideo);
        }

        [Test]
        public void HideCamera_WhenPresenterCameraIsMuted()
        {
            AddParticipant(BOT, V2_METADATA);
            LKParticipant presenter = AddParticipant(PRESENTER);
            IVideoStream camera = Subscribe(presenter, AddTrack(presenter, "TR_cam", TrackKind.KindVideo, TrackSource.SourceCamera, muted: true));
            LivekitPlayer p = NewV2Player();

            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            AssertComposite(p.LastTexture());

            videoStreams.Received().ActiveStream(new StreamKey(PRESENTER, "TR_cam"));
            camera.DidNotReceive().DecodeLastFrame();
        }

        [Test]
        public void LogBadMetadataOnce()
        {
            LogAssert.Expect(LogType.Warning, new Regex("metadata"));
            LKParticipant bot = AddParticipant(BOT);
            LivekitPlayer p = NewV2Player();
            p.OpenMedia(LivekitAddress.CurrentStream());

            SetMetadata(bot, "{");
            p.EnsureVideoIsPlaying();
            SetMetadata(bot, "{\"slide\":1}");
            p.EnsureVideoIsPlaying();

            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ReleasePresentationStreams_OnReconnect()
        {
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LKParticipant presenter = AddParticipant(PRESENTER);
            Subscribe(presenter, AddTrack(presenter, "TR_cam", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewV2Player();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            room.ConnectionUpdated += Raise.Event<ConnectionDelegate>(room, ConnectionUpdate.Reconnected, null);
            p.EnsureVideoIsPlaying();

            videoStreams.Received().Release(new StreamKey(BOT, "TR_pv"));
            videoStreams.Received().Release(new StreamKey(PRESENTER, "TR_cam"));
        }

        [Test]
        public void OpenCameraStream_WhenRemoteCameraTrackSubscribed()
        {
            LivekitPlayer p = NewLegacyPlayer();
            LKParticipant streamer = AddParticipant(STREAMER);
            Texture2D cameraFrame = SubscribeWithFrame(streamer, AddTrack(streamer, CAMERA_SID, TrackKind.KindVideo, TrackSource.SourceCamera));

            p.OpenMedia(LivekitAddress.CurrentStream());

            Assert.That(p.IsVideoOpened, Is.True);
            Assert.That(p.LastTexture(), Is.SameAs(cameraFrame));
        }

        [Test]
        public void OpenScreenShareStream_WhenScreenShareTrackSubscribed()
        {
            LivekitPlayer p = NewLegacyPlayer();
            LKParticipant streamer = AddParticipant(STREAMER);
            Texture2D screenFrame = SubscribeWithFrame(streamer, AddTrack(streamer, SCREEN_SID, TrackKind.KindVideo, TrackSource.SourceScreenshare));

            p.OpenMedia(LivekitAddress.CurrentStream());

            Assert.That(p.IsVideoOpened, Is.True);
            Assert.That(p.LastTexture(), Is.SameAs(screenFrame));
        }

        [Test]
        [Ignore("Fails on dev: initial selection picks the unsubscribed screen share over the subscribed camera")]
        public void ShowCamera_WhenScreenShareIsAnnouncedButNotYetSubscribed()
        {
            LivekitPlayer p = NewLegacyPlayer();
            LKParticipant streamer = AddParticipant(STREAMER);
            Texture2D cameraFrame = SubscribeWithFrame(streamer, AddTrack(streamer, CAMERA_SID, TrackKind.KindVideo, TrackSource.SourceCamera));
            AddTrack(streamer, SCREEN_SID, TrackKind.KindVideo, TrackSource.SourceScreenshare);

            p.OpenMedia(LivekitAddress.CurrentStream());

            Assert.That(p.IsVideoOpened, Is.True, "an unsubscribed screen share must not shadow an available camera");
            Assert.That(p.LastTexture(), Is.SameAs(cameraFrame));
        }

        [Test]
        [Ignore("Fails on dev: the follow pass switches to the unsubscribed screen share")]
        public void KeepCamera_WhenScreenShareArrivesUnsubscribedMidStream()
        {
            LivekitPlayer p = NewLegacyPlayer();
            LKParticipant streamer = AddParticipant(STREAMER);
            Texture2D cameraFrame = SubscribeWithFrame(streamer, AddTrack(streamer, CAMERA_SID, TrackKind.KindVideo, TrackSource.SourceCamera));
            p.OpenMedia(LivekitAddress.CurrentStream());
            Assert.That(p.IsVideoOpened, Is.True);

            AddTrack(streamer, SCREEN_SID, TrackKind.KindVideo, TrackSource.SourceScreenshare);
            p.EnsureVideoIsPlaying();

            Assert.That(p.IsVideoOpened, Is.True, "a still-subscribing screen share must not blank the playing camera");
            Assert.That(p.LastTexture(), Is.SameAs(cameraFrame));
        }

        [Test]
        public void SwitchToScreenShare_WhenItBecomesSubscribed()
        {
            LivekitPlayer p = NewLegacyPlayer();
            LKParticipant streamer = AddParticipant(STREAMER);
            SubscribeWithFrame(streamer, AddTrack(streamer, CAMERA_SID, TrackKind.KindVideo, TrackSource.SourceCamera));
            p.OpenMedia(LivekitAddress.CurrentStream());

            Texture2D screenFrame = SubscribeWithFrame(streamer, AddTrack(streamer, SCREEN_SID, TrackKind.KindVideo, TrackSource.SourceScreenshare));
            p.EnsureVideoIsPlaying();

            Assert.That(p.IsVideoOpened, Is.True);
            Assert.That(p.LastTexture(), Is.SameAs(screenFrame), "a subscribed screen share must take priority over the camera");
        }

        [Test]
        public void RecoverStream_WhenTrackSubscribedArrivesAfterOpen()
        {
            LivekitPlayer p = NewLegacyPlayer();
            LKParticipant streamer = AddParticipant(STREAMER);
            TrackPublication camera = AddTrack(streamer, CAMERA_SID, TrackKind.KindVideo, TrackSource.SourceCamera);
            p.OpenMedia(LivekitAddress.CurrentStream());
            Assert.That(p.IsVideoOpened, Is.False, "nothing renders until the track is subscribed");

            Texture2D cameraFrame = SubscribeWithFrame(streamer, camera);
            p.EnsureVideoIsPlaying();

            Assert.That(p.IsVideoOpened, Is.True);
            Assert.That(p.LastTexture(), Is.SameAs(cameraFrame));
        }

        [Test]
        [Ignore("Fails on dev: the follow pass keeps the departed caster's stale stream")]
        public void SwitchToNewCaster_AfterCurrentCasterLeavesFollowingRoomReset()
        {
            LivekitPlayer p = NewLegacyPlayer();
            LKParticipant streamer = AddParticipant(STREAMER);
            Texture2D frameA = SubscribeWithFrame(streamer, AddTrack(streamer, CAMERA_SID, TrackKind.KindVideo, TrackSource.SourceCamera));
            p.OpenMedia(LivekitAddress.CurrentStream());
            Assert.That(p.LastTexture(), Is.SameAs(frameA));

            resolvedStreams.Remove(new StreamKey(STREAMER, CAMERA_SID));
            remoteParticipants.Remove(STREAMER);
            participantsHub.RemoteParticipant(STREAMER).Returns((LKParticipant?)null);
            LKParticipant newStreamer = AddParticipant(NEW_STREAMER);
            Texture2D frameB = SubscribeWithFrame(newStreamer, AddTrack(newStreamer, NEW_CAMERA_SID, TrackKind.KindVideo, TrackSource.SourceCamera));
            p.EnsureVideoIsPlaying();

            Assert.That(p.IsVideoOpened, Is.True);
            Assert.That(p.LastTexture(), Is.SameAs(frameB));
        }

        [Test]
        public void RenderNothing_WhenNoVideoTracksAvailable()
        {
            LivekitPlayer p = NewLegacyPlayer();

            p.OpenMedia(LivekitAddress.CurrentStream());

            Assert.That(p.IsVideoOpened, Is.False);
            Assert.That(p.LastTexture(), Is.Null);
        }

        private (LivekitPlayer player, LKParticipant bot) DrawSecondFrame()
        {
            LKParticipant bot = AddParticipant(BOT, V2("null", "idle"));
            IVideoStream video = Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            Texture2D first = NewFrame();
            video.DecodeLastFrame().Returns(first);
            LivekitPlayer p = NewV2Player();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            AssertComposite(p.LastTexture());

            SetMetadata(bot, V2("0", "playing"));
            p.EnsureVideoIsPlaying();
            p.LastTexture();
            Assert.IsFalse(p.lastComposeDrewVideo);

            Texture2D second = NewFrame();
            video.DecodeLastFrame().Returns(second);
            SetMetadata(bot, V2("0", "playing", 1));
            p.EnsureVideoIsPlaying();
            p.LastTexture();

            return (p, bot);
        }

        private static string V2(string playingVideoIndex, string videoState, int currentSlide = 0) =>
            $"{{\"role\":\"presentation\",\"currentSlide\":{currentSlide},\"slide\":{{\"url\":\"{SLIDE_URL}\",\"width\":1920,\"height\":1080}},\"presenterIdentity\":\"{PRESENTER}\","
            + $"\"playingVideoIndex\":{playingVideoIndex},\"videoState\":\"{videoState}\",\"slideVideos\":[{SLIDE_VIDEO},{SLIDE_VIDEO}],"
            + "\"overlay\":{\"x\":0,\"y\":1,\"size\":\"small\"}}";

        private static void AssertComposite(Texture? texture)
        {
            Assert.IsInstanceOf<RenderTexture>(texture);
            Assert.AreEqual(1920, texture!.width);
            Assert.AreEqual(1080, texture.height);
        }

        private LKParticipant AddParticipant(string identity, string metadata = "")
        {
            var participant = new LKParticipant();

            typeof(LKParticipant).GetField("info", BindingFlags.Instance | BindingFlags.NonPublic)!
                                 .SetValue(participant, new ParticipantInfo { Identity = identity, Metadata = metadata });

            remoteParticipants[identity] = participant;
            participantsHub.RemoteParticipant(identity).Returns(participant);
            return participant;
        }

        private void SetMetadata(LKParticipant participant, string metadata)
        {
            participant.UpdateMeta(metadata);
            participantsHub.UpdatesFromParticipant += Raise.Event<ParticipantDelegate>(participant, UpdateFromParticipant.MetadataChanged);
        }

        private static TrackPublication AddTrack(LKParticipant participant, string sid, TrackKind kind, TrackSource source, string name = "", bool muted = false)
        {
            var track = new TrackPublication();

            typeof(TrackPublication).GetField("info", BindingFlags.Instance | BindingFlags.NonPublic)!
                                    .SetValue(track, new TrackPublicationInfo { Sid = sid, Kind = kind, Source = source, Name = name, Muted = muted });

            participant.AddTrack(track);
            return track;
        }

        private IVideoStream Subscribe(LKParticipant participant, TrackPublication track)
        {
            typeof(TrackPublication).GetMethod("UpdateTrack", BindingFlags.Instance | BindingFlags.NonPublic)!
                                    .Invoke(track, new object[] { Substitute.For<ITrack>() });

            IVideoStream stream = Substitute.For<IVideoStream>();
            resolvedStreams[new StreamKey(participant.Identity, track.Sid)] = new Owned<IVideoStream>(stream).Downgrade();
            room.TrackSubscribed += Raise.Event<SubscribeDelegate>(null!, track, participant);
            return stream;
        }

        private Texture2D SubscribeWithFrame(LKParticipant participant, TrackPublication track)
        {
            Texture2D frame = NewFrame();
            Subscribe(participant, track).DecodeLastFrame().Returns(frame);
            return frame;
        }

        private Texture2D NewFrame()
        {
            var frame = new Texture2D(2, 2);
            created.Add(frame);
            return frame;
        }

        private LivekitPlayer NewV2Player()
        {
            var material = new Material(Shader.Find("DCL/PresentationCompositor"));
            created.Add(material);
            var slideCache = new SlideTextureCache(Substitute.For<IWebRequestController>());
            slideCaches.Add(slideCache);
            player = new LivekitPlayer(room, () => true, null, slideCache, material);
            return player;
        }

        private LivekitPlayer NewLegacyPlayer()
        {
            player = new LivekitPlayer(room, () => true, null, null, null);
            return player;
        }

        private sealed class FakeActiveSpeakers : IActiveSpeakers
        {
            public int Count => 0;

            public event Action Updated { add { } remove { } }

            public IEnumerator<string> GetEnumerator() =>
                ((IEnumerable<string>)Array.Empty<string>()).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                GetEnumerator();
        }
    }
}
