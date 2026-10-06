using Cysharp.Threading.Tasks;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Rooms.Nulls;
using DCL.Tests.Editor;
using DCL.WebRequests;
using LiveKit.Proto;
using LiveKit.Rooms;
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
using System.Collections.Generic;
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
        private const string OTHER_BOT = "presentation-bot:other:1";
        private const string PRESENTER = "stream:place:1";
        private const string SECOND_PRESENTER = "stream:place:2";
        private const string OTHER = "0xOTHER";
        private const string STREAMER = "0xSTREAMER";
        private const string CAMERA_SID = "TR_camera";
        private const string SCREEN_SID = "TR_screen";
        private const string SLIDE_URL = "https://example.com/slide.png";
        private const string ALLOWED_SLIDES = "https://cast-presenter-service.decentraland.org/presentations/0f8fad5b-d9cb-469f-a165-70867728950e/slides/";
        private const string BOT_SLIDE_URL = ALLOWED_SLIDES + "0123456789abcdef.png";
        private const string OTHER_BOT_SLIDE_URL = ALLOWED_SLIDES + "fedcba9876543210.png";
        private const string LEGACY_METADATA = "{\"role\":\"presentation\",\"presentationId\":\"p1\"}";
        private const string V2_METADATA = "{\"role\":\"presentation\",\"slide\":{\"url\":\"" + SLIDE_URL + "\",\"width\":1920,\"height\":1080},\"presenterIdentity\":\"" + PRESENTER
                                           + "\",\"playingVideoIndex\":null,\"videoState\":\"idle\",\"slideVideos\":[],\"overlay\":{\"x\":0,\"y\":1,\"size\":\"small\"}}";
        private const string SLIDE_VIDEO = "{\"geometry\":{\"x\":480,\"y\":270,\"width\":960,\"height\":540}}";

        private static readonly Vector2 FLIPPED = new (1f, -1f);

        private IRoom room = null!;
        private IParticipantsHub participantsHub = null!;
        private IVideoStreams videoStreams = null!;
        private IDecentralandUrlsSource decentralandUrlsSource = null!;
        private Dictionary<string, LKParticipant> remoteParticipants = null!;
        private Dictionary<StreamKey, Weak<IVideoStream>> resolvedStreams = null!;
        private List<Object> created = null!;
        private List<SlideTextureCache> slideCaches = null!;
        private List<LivekitPlayer> players = null!;
        private float now;

        [SetUp]
        public void SetUp()
        {
            room = Substitute.For<IRoom>();
            participantsHub = Substitute.For<IParticipantsHub>();
            videoStreams = Substitute.For<IVideoStreams>();
            decentralandUrlsSource = Substitute.For<IDecentralandUrlsSource>();
            decentralandUrlsSource.Url(DecentralandUrl.CastPresenterService).Returns("https://cast-presenter-service.decentraland.org");
            remoteParticipants = new Dictionary<string, LKParticipant>();
            resolvedStreams = new Dictionary<StreamKey, Weak<IVideoStream>>();
            created = new List<Object>();
            slideCaches = new List<SlideTextureCache>();
            players = new List<LivekitPlayer>();
            now = 0f;

            room.Participants.Returns(participantsHub);
            room.VideoStreams.Returns(videoStreams);
            room.ActiveSpeakers.Returns(NullActiveSpeakers.INSTANCE);
            room.Info.ConnectionState.Returns(LKConnectionState.ConnConnected);
            participantsHub.RemoteParticipantIdentities().Returns(remoteParticipants);
            videoStreams.ActiveStream(Arg.Any<StreamKey>())
                        .Returns(ci => resolvedStreams.TryGetValue(ci.Arg<StreamKey>(), out Weak<IVideoStream> weak) ? weak : Weak<IVideoStream>.Null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (LivekitPlayer p in players)
                p.Dispose();

            foreach (SlideTextureCache cache in slideCaches)
                cache.Dispose();

            foreach (Object obj in created)
                Object.DestroyImmediate(obj);
        }

        [Test]
        public void ComposePresentation_WhenBotMetadataHasSlide()
        {
            // Arrange
            AddParticipant(BOT, V2_METADATA);
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.IsTrue(p.IsVideoOpened);
            Assert.AreEqual(Vector2.one, p.CurrentTextureScale);
            AssertComposite(p.LastTexture());
        }

        [Test]
        public void OpenPresentationVideoByName_WhenComposing()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            // Assert
            videoStreams.Received().ActiveStream(new StreamKey(BOT, "TR_pv"));
        }

        [Test]
        public void OpenPresenterCamera_WhenPresenterIdentityIsSet()
        {
            // Arrange
            AddParticipant(BOT, V2_METADATA);
            LKParticipant presenter = AddParticipant(PRESENTER);
            Subscribe(presenter, AddTrack(presenter, "TR_cam", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            // Assert
            videoStreams.Received().ActiveStream(new StreamKey(PRESENTER, "TR_cam"));
        }

        [Test]
        public void NotOpenOtherCameras_WhenComposing()
        {
            // Arrange
            LKParticipant other = AddParticipant(OTHER);
            Subscribe(other, AddTrack(other, "TR_other", TrackKind.KindVideo, TrackSource.SourceCamera));
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LKParticipant presenter = AddParticipant(PRESENTER);
            Subscribe(presenter, AddTrack(presenter, "TR_cam", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            p.EnsureVideoIsPlaying();

            // Assert
            videoStreams.DidNotReceive().ActiveStream(new StreamKey(OTHER, "TR_other"));
        }

        [Test]
        public void KeepLegacyTrack_WhenMetadataHasNoSlide()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, LEGACY_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_p", TrackKind.KindVideo, TrackSource.SourceCamera, "presentation"));
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            // Assert
            videoStreams.Received().ActiveStream(new StreamKey(BOT, "TR_p"));
            Assert.IsTrue(p.IsVideoOpened);
            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);
        }

        [Test]
        public void NeverSelectPresentationVideo_OnLegacyPath()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, LEGACY_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LKParticipant other = AddParticipant(OTHER);
            Subscribe(other, AddTrack(other, "TR_other", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            // Assert
            videoStreams.Received().ActiveStream(new StreamKey(OTHER, "TR_other"));
            videoStreams.DidNotReceive().ActiveStream(new StreamKey(BOT, "TR_pv"));
        }

        [Test]
        public void SwitchToComposite_WhenMetadataChangesMidSession()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, LEGACY_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_p", TrackKind.KindVideo, TrackSource.SourceCamera, "presentation"));
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);

            // Act
            SetMetadata(bot, V2_METADATA);
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.AreEqual(Vector2.one, p.CurrentTextureScale);
            AssertComposite(p.LastTexture());
        }

        [Test]
        public void FallBackToLegacy_WhenSlideDisappears()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_p", TrackKind.KindVideo, TrackSource.SourceCamera, "presentation"));
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            Texture? composite = p.LastTexture();
            AssertComposite(composite);
            videoStreams.DidNotReceive().ActiveStream(new StreamKey(BOT, "TR_p"));

            // Act
            SetMetadata(bot, LEGACY_METADATA);
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);
            Assert.IsTrue(p.IsVideoOpened);
            Assert.IsTrue(composite == null);
            videoStreams.Received().ActiveStream(new StreamKey(BOT, "TR_p"));
        }

        [Test]
        public void NotCompose_ForPinnedNonBotAddress()
        {
            // Arrange
            LKParticipant other = AddParticipant(OTHER);
            Subscribe(other, AddTrack(other, "TR_other", TrackKind.KindVideo, TrackSource.SourceCamera));
            AddParticipant(BOT, V2_METADATA);
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.FromUserStream(new UserStream(OTHER, "TR_other")));
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);
            videoStreams.Received().ActiveStream(new StreamKey(OTHER, "TR_other"));
        }

        [Test]
        public void NotComposeOtherBot_WhenAddressIsPinnedToLegacyBot()
        {
            // Arrange
            AddParticipant(OTHER_BOT, V2_METADATA);
            LKParticipant bot = AddParticipant(BOT, LEGACY_METADATA);
            Subscribe(bot, AddTrack(bot, SCREEN_SID, TrackKind.KindVideo, TrackSource.SourceScreenshare));
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.FromUserStream(new UserStream(BOT, SCREEN_SID)));
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);
            videoStreams.Received().ActiveStream(new StreamKey(BOT, SCREEN_SID));
        }

        [Test]
        public void ComposePinnedBot_WhenAnotherBotEnumeratesFirst()
        {
            // Arrange
            AddParticipant(OTHER_BOT, V2("null", "idle", width: 1280, height: 720));
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.FromUserStream(new UserStream(BOT, "TR_pv")));
            p.EnsureVideoIsPlaying();

            // Assert
            AssertComposite(p.LastTexture());
            videoStreams.Received().ActiveStream(new StreamKey(BOT, "TR_pv"));
        }

        [Test]
        public void NotComposeOtherBot_WhenPinnedBotLeaves()
        {
            // Arrange
            LKParticipant otherBot = AddParticipant(OTHER_BOT, LEGACY_METADATA);
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.FromUserStream(new UserStream(BOT, "TR_old")));
            p.EnsureVideoIsPlaying();
            AssertComposite(p.LastTexture());

            // Act
            RemoveParticipant(bot);
            p.EnsureVideoIsPlaying();
            SetMetadata(otherBot, V2_METADATA);
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.AreNotEqual(Vector2.one, p.CurrentTextureScale);
            Assert.IsNotInstanceOf<RenderTexture>(p.LastTexture());
        }

        [Test]
        public void ReopenPinnedLegacyTrack_WhenCompositionEnds()
        {
            // Arrange
            LKParticipant otherBot = AddParticipant(OTHER_BOT, LEGACY_METADATA);
            Subscribe(otherBot, AddTrack(otherBot, "TR_other_bot", TrackKind.KindVideo, TrackSource.SourceScreenshare));
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, SCREEN_SID, TrackKind.KindVideo, TrackSource.SourceScreenshare));
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.FromUserStream(new UserStream(BOT, SCREEN_SID)));
            p.EnsureVideoIsPlaying();
            AssertComposite(p.LastTexture());

            // Act
            SetMetadata(bot, LEGACY_METADATA);
            p.EnsureVideoIsPlaying();

            // Assert
            videoStreams.Received().ActiveStream(new StreamKey(BOT, SCREEN_SID));
            videoStreams.DidNotReceive().ActiveStream(new StreamKey(OTHER_BOT, "TR_other_bot"));
        }

        [Test]
        public void ReopenPinnedLegacyTrack_WhenCompositionEndsAfterAnEarlierFallback()
        {
            // Arrange
            LKParticipant otherBot = AddParticipant(OTHER_BOT, LEGACY_METADATA);
            Subscribe(otherBot, AddTrack(otherBot, "TR_other_bot", TrackKind.KindVideo, TrackSource.SourceScreenshare));
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.FromUserStream(new UserStream(BOT, SCREEN_SID)));
            p.EnsureVideoIsPlaying();

            LKParticipant bot = LiveKitTestObjects.NewParticipant(BOT, V2_METADATA);
            remoteParticipants[BOT] = bot;
            participantsHub.RemoteParticipant(BOT).Returns(bot);
            Subscribe(bot, AddTrack(bot, SCREEN_SID, TrackKind.KindVideo, TrackSource.SourceScreenshare));
            participantsHub.UpdatesFromParticipant += Raise.Event<ParticipantDelegate>(bot, UpdateFromParticipant.Connected);
            p.EnsureVideoIsPlaying();
            AssertComposite(p.LastTexture());
            videoStreams.ClearReceivedCalls();

            // Act
            SetMetadata(bot, LEGACY_METADATA);
            p.EnsureVideoIsPlaying();

            // Assert
            videoStreams.Received().ActiveStream(new StreamKey(BOT, SCREEN_SID));
            videoStreams.DidNotReceive().ActiveStream(new StreamKey(OTHER_BOT, "TR_other_bot"));
        }

        [Test]
        public void RebindPresentationVideo_WhenComposingBotChangesWithSameMetadata()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LKParticipant otherBot = LiveKitTestObjects.NewParticipant(OTHER_BOT, V2_METADATA);
            AddTrack(otherBot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME);
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            // Act
            remoteParticipants.Clear();
            remoteParticipants[OTHER_BOT] = otherBot;
            remoteParticipants[BOT] = bot;
            participantsHub.RemoteParticipant(OTHER_BOT).Returns(otherBot);
            participantsHub.UpdatesFromParticipant += Raise.Event<ParticipantDelegate>(otherBot, UpdateFromParticipant.Connected);
            p.EnsureVideoIsPlaying();

            // Assert
            videoStreams.Received().ActiveStream(new StreamKey(OTHER_BOT, "TR_pv"));
        }

        [Test]
        public void NotReadParticipantMetadata_WhenPinnedToNonBot()
        {
            // Arrange
            LKParticipant other = AddParticipant(OTHER, "{");
            Subscribe(other, AddTrack(other, "TR_other", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.FromUserStream(new UserStream(OTHER, "TR_other")));
            p.EnsureVideoIsPlaying();

            // Assert
            LogAssert.NoUnexpectedReceived();
            videoStreams.Received().ActiveStream(new StreamKey(OTHER, "TR_other"));
        }

        [Test]
        public void NotComposeBot_WhenPinnedStreamFallsBackToCurrentStream()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            LKParticipant other = AddParticipant(OTHER);
            AddTrack(other, "TR_other", TrackKind.KindVideo, TrackSource.SourceCamera);
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.FromUserStream(new UserStream(OTHER, "TR_other")));
            p.EnsureVideoIsPlaying();
            p.EnsureVideoIsPlaying();

            // Act
            SetMetadata(bot, V2("null", "idle", 1));
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.AreNotEqual(Vector2.one, p.CurrentTextureScale);
            Assert.IsNotInstanceOf<RenderTexture>(p.LastTexture());
        }

        [Test]
        public void ThrottleSlidesPerBot_WhenTwoPlayersShareTheCache()
        {
            // Arrange
            IWebRequestController controller = Substitute.For<IWebRequestController>();
            controller.SendAsync<GetTextureWebRequest, GetTextureArguments, SlideTextureCache.SlideTextureOp, Texture2D>(default, default)
                      .ReturnsForAnyArgs(_ => new UniTaskCompletionSource<Texture2D?>().Task);
            var sharedCache = new SlideTextureCache(controller, decentralandUrlsSource, static () => 0f);
            slideCaches.Add(sharedCache);
            AddParticipant(BOT, V2("null", "idle", slideUrl: BOT_SLIDE_URL));
            AddParticipant(OTHER_BOT, V2("null", "idle", slideUrl: OTHER_BOT_SLIDE_URL));
            LivekitPlayer first = NewPlayer(sharedCache);
            LivekitPlayer second = NewPlayer(sharedCache);
            first.OpenMedia(LivekitAddress.FromUserStream(new UserStream(BOT, "TR_old")));
            second.OpenMedia(LivekitAddress.FromUserStream(new UserStream(OTHER_BOT, "TR_old")));
            first.EnsureVideoIsPlaying();
            second.EnsureVideoIsPlaying();

            // Act
            first.LastTexture();
            second.LastTexture();

            // Assert
            controller.ReceivedWithAnyArgs(2).SendAsync<GetTextureWebRequest, GetTextureArguments, SlideTextureCache.SlideTextureOp, Texture2D>(default, default);
        }

        [Test]
        public void HideCamera_WhenPresenterCameraIsMuted()
        {
            // Arrange
            AddParticipant(BOT, V2_METADATA);
            LKParticipant presenter = AddParticipant(PRESENTER);
            IVideoStream camera = Subscribe(presenter, AddTrack(presenter, "TR_cam", TrackKind.KindVideo, TrackSource.SourceCamera, muted: true));
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            AssertComposite(p.LastTexture());

            // Assert
            videoStreams.Received().ActiveStream(new StreamKey(PRESENTER, "TR_cam"));
            camera.DidNotReceive().DecodeLastFrame();
        }

        [Test]
        public void DecodePresenterCamera_WhenCameraIsLive()
        {
            // Arrange
            AddParticipant(BOT, V2_METADATA);
            LKParticipant presenter = AddParticipant(PRESENTER);
            IVideoStream camera = Subscribe(presenter, AddTrack(presenter, "TR_cam", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            AssertComposite(p.LastTexture());

            // Assert
            camera.Received().DecodeLastFrame();
        }

        [Test]
        public void RebindPresenterCamera_WhenPresenterIdentityChanges()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            LKParticipant secondPresenter = AddParticipant(SECOND_PRESENTER);
            Subscribe(secondPresenter, AddTrack(secondPresenter, "TR_cam2", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            videoStreams.DidNotReceive().ActiveStream(new StreamKey(SECOND_PRESENTER, "TR_cam2"));

            // Act
            SetMetadata(bot, V2("null", "idle", presenter: SECOND_PRESENTER));
            p.EnsureVideoIsPlaying();

            // Assert
            videoStreams.Received().ActiveStream(new StreamKey(SECOND_PRESENTER, "TR_cam2"));
        }

        [Test]
        public void FallBackToLegacy_WhenBotLeaves()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            LKParticipant streamer = AddParticipant(STREAMER);
            Subscribe(streamer, AddTrack(streamer, CAMERA_SID, TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            AssertComposite(p.LastTexture());

            // Act
            RemoveParticipant(bot);
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);
            videoStreams.Received().ActiveStream(new StreamKey(STREAMER, CAMERA_SID));
        }

        [Test]
        public void LogBadMetadataOnce()
        {
            // Arrange
            LogAssert.Expect(LogType.Warning, new Regex("metadata"));
            LKParticipant bot = AddParticipant(BOT);
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.CurrentStream());

            // Act
            SetMetadata(bot, "{");
            p.EnsureVideoIsPlaying();
            SetMetadata(bot, "{\"slide\":1}");
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.AreEqual(FLIPPED, p.CurrentTextureScale);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ReleasePresentationStreams_OnReconnect()
        {
            // Act
            ComposeThenReconnect();

            // Assert
            videoStreams.Received().Release(new StreamKey(BOT, "TR_pv"));
            videoStreams.Received().Release(new StreamKey(PRESENTER, "TR_cam"));
        }

        [Test]
        public void ReopenPresentationStreams_AfterReconnect()
        {
            // Act
            ComposeThenReconnect();

            // Assert
            videoStreams.Received(2).ActiveStream(new StreamKey(BOT, "TR_pv"));
            videoStreams.Received(2).ActiveStream(new StreamKey(PRESENTER, "TR_cam"));
        }

        [Test]
        public void NotThrow_WhenOpeningMediaDuringReconnect()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            LivekitPlayer p = NewPlayer();
            object? connectedInfo = LiveKitTestObjects.PARTICIPANT_INFO.GetValue(bot);
            room.Info.ConnectionState.Returns(LKConnectionState.ConnReconnecting);
            LiveKitTestObjects.PARTICIPANT_INFO.SetValue(bot, null);

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());

            LiveKitTestObjects.PARTICIPANT_INFO.SetValue(bot, connectedInfo);
            room.Info.ConnectionState.Returns(LKConnectionState.ConnConnected);
            p.EnsureVideoIsPlaying();

            // Assert
            AssertComposite(p.LastTexture());
        }

        [Test]
        public void OpenCameraStream_WhenRemoteCameraTrackSubscribed()
        {
            // Arrange
            LivekitPlayer p = NewPlayer();
            LKParticipant streamer = AddParticipant(STREAMER);
            Texture2D cameraFrame = SubscribeWithFrame(streamer, AddTrack(streamer, CAMERA_SID, TrackKind.KindVideo, TrackSource.SourceCamera));

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());

            // Assert
            Assert.That(p.IsVideoOpened, Is.True);
            Assert.That(p.LastTexture(), Is.SameAs(cameraFrame));
        }

        [Test]
        public void OpenScreenShareStream_WhenScreenShareTrackSubscribed()
        {
            // Arrange
            LivekitPlayer p = NewPlayer();
            LKParticipant streamer = AddParticipant(STREAMER);
            Texture2D screenFrame = SubscribeWithFrame(streamer, AddTrack(streamer, SCREEN_SID, TrackKind.KindVideo, TrackSource.SourceScreenshare));

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());

            // Assert
            Assert.That(p.IsVideoOpened, Is.True);
            Assert.That(p.LastTexture(), Is.SameAs(screenFrame));
        }

        [Test]
        public void SwitchToScreenShare_WhenItBecomesSubscribed()
        {
            // Arrange
            LivekitPlayer p = NewPlayer();
            LKParticipant streamer = AddParticipant(STREAMER);
            SubscribeWithFrame(streamer, AddTrack(streamer, CAMERA_SID, TrackKind.KindVideo, TrackSource.SourceCamera));
            p.OpenMedia(LivekitAddress.CurrentStream());

            // Act
            Texture2D screenFrame = SubscribeWithFrame(streamer, AddTrack(streamer, SCREEN_SID, TrackKind.KindVideo, TrackSource.SourceScreenshare));
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.That(p.IsVideoOpened, Is.True);
            Assert.That(p.LastTexture(), Is.SameAs(screenFrame), "a subscribed screen share must take priority over the camera");
        }

        [Test]
        public void RecoverStream_WhenTrackSubscribedArrivesAfterOpen()
        {
            // Arrange
            LivekitPlayer p = NewPlayer();
            LKParticipant streamer = AddParticipant(STREAMER);
            TrackPublication camera = AddTrack(streamer, CAMERA_SID, TrackKind.KindVideo, TrackSource.SourceCamera);
            p.OpenMedia(LivekitAddress.CurrentStream());
            Assert.That(p.IsVideoOpened, Is.False, "nothing renders until the track is subscribed");

            // Act
            Texture2D cameraFrame = SubscribeWithFrame(streamer, camera);
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.That(p.IsVideoOpened, Is.True);
            Assert.That(p.LastTexture(), Is.SameAs(cameraFrame));
        }

        [Test]
        public void RenderNothing_WhenNoVideoTracksAvailable()
        {
            // Arrange
            LivekitPlayer p = NewPlayer();

            // Act
            p.OpenMedia(LivekitAddress.CurrentStream());

            // Assert
            Assert.That(p.IsVideoOpened, Is.False);
            Assert.That(p.LastTexture(), Is.Null);
        }

        [Test]
        public void StopComposingUntilReconnect_WhenRoomDisconnects()
        {
            // Arrange
            AddParticipant(BOT, V2_METADATA);
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            // Act
            room.Info.ConnectionState.Returns(LKConnectionState.ConnDisconnected);
            p.EnsureVideoIsPlaying();
            bool openedWhileDisconnected = p.IsVideoOpened;
            Texture? textureWhileDisconnected = p.LastTexture();
            room.Info.ConnectionState.Returns(LKConnectionState.ConnConnected);
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.IsFalse(openedWhileDisconnected);
            Assert.IsNull(textureWhileDisconnected);
            Assert.IsTrue(p.IsVideoOpened);
            AssertComposite(p.LastTexture());
        }

        [Test]
        public void ComposeOnce_WhenLastTextureIsReadTwiceInAFrame()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, V2("0", "playing"));
            SubscribeWithFrame(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            // Act
            Texture? first = p.LastTexture();
            Texture? second = p.LastTexture();

            // Assert
            Assert.AreSame(first, second);
            Assert.AreEqual(1, p.compositorBlitCount);
        }

        [Test]
        public void NotDecodePresentationVideo_WhenRectIsHidden()
        {
            // Arrange
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            IVideoStream video = Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            // Act & Assert
            AssertComposite(p.LastTexture());

            video.DidNotReceive().DecodeLastFrame();
        }

        [Test]
        public void KeepLastSlide_WhileNextSlideLoads()
        {
            // Arrange
            IWebRequestController controller = Substitute.For<IWebRequestController>();
            SlideTextureCache slideCache = NewSlideCacheHolding(controller, BOT_SLIDE_URL, () => now);
            LKParticipant bot = AddParticipant(BOT, V2("null", "idle", slideUrl: BOT_SLIDE_URL));
            LivekitPlayer p = NewPlayer(slideCache);
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            p.LastTexture();

            // Act
            now = 1f;
            SetMetadata(bot, V2("null", "idle", currentSlide: 1, slideUrl: OTHER_BOT_SLIDE_URL));
            p.EnsureVideoIsPlaying();
            p.LastTexture();

            // Assert
            SlideRequest(controller.ReceivedWithAnyArgs(2));
            Assert.AreEqual(1, p.compositorBlitCount);
        }

        [Test]
        public void DestroySlides_WhenLastComposingPlayerStops()
        {
            // Arrange
            IWebRequestController controller = Substitute.For<IWebRequestController>();
            SlideTextureCache slideCache = NewSlideCacheHolding(controller, BOT_SLIDE_URL, static () => 0f);
            Texture2D? slide = slideCache.GetOrRequest(BOT_SLIDE_URL, BOT);
            LKParticipant bot = AddParticipant(BOT, V2("null", "idle", slideUrl: BOT_SLIDE_URL));
            LivekitPlayer p = NewPlayer(slideCache);
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();
            p.LastTexture();
            slideCache.EndComposing();

            // Act
            SetMetadata(bot, LEGACY_METADATA);
            p.EnsureVideoIsPlaying();

            // Assert
            Assert.IsTrue(slide == null);
        }

        [Test]
        public void KeepSlides_WhileAnotherPlayerComposes()
        {
            // Arrange
            IWebRequestController controller = Substitute.For<IWebRequestController>();
            SlideTextureCache slideCache = NewSlideCacheHolding(controller, BOT_SLIDE_URL, static () => 0f);
            Texture2D? slide = slideCache.GetOrRequest(BOT_SLIDE_URL, BOT);
            AddParticipant(BOT, V2("null", "idle", slideUrl: BOT_SLIDE_URL));
            LivekitPlayer first = NewPlayer(slideCache);
            LivekitPlayer second = NewPlayer(slideCache);
            first.OpenMedia(LivekitAddress.CurrentStream());
            second.OpenMedia(LivekitAddress.CurrentStream());
            slideCache.EndComposing();

            // Act
            first.CloseCurrentStream();

            // Assert
            Assert.IsTrue(slide != null);
        }

        [Test]
        public void KeepShownSlide_WhenClearedThrottledWhileNextSlideLoads()
        {
            // Arrange
            var shown = new Texture2D(2, 2);
            IWebRequestController controller = Substitute.For<IWebRequestController>();
            SlideRequest(controller).ReturnsForAnyArgs(UniTask.FromResult<Texture2D?>(shown), UniTask.FromResult<Texture2D?>(new Texture2D(2, 2)),
                UniTask.FromResult<Texture2D?>(new Texture2D(2, 2)), new UniTaskCompletionSource<Texture2D?>().Task);
            var slideCache = new SlideTextureCache(controller, decentralandUrlsSource, () => now);
            slideCaches.Add(slideCache);
            slideCache.BeginComposing();
            slideCache.GetOrRequest(SlideUrl(1), BOT);
            LKParticipant bot = AddParticipant(BOT, V2("null", "idle", slideUrl: SlideUrl(1)));
            LKParticipant otherBot = AddParticipant(OTHER_BOT, V2("null", "idle", slideUrl: SlideUrl(2)));
            LivekitPlayer first = NewPlayer(slideCache);
            LivekitPlayer second = NewPlayer(slideCache);
            first.OpenMedia(LivekitAddress.FromUserStream(new UserStream(BOT, "TR_pv")));
            second.OpenMedia(LivekitAddress.FromUserStream(new UserStream(OTHER_BOT, "TR_pv")));
            slideCache.EndComposing();
            first.LastTexture();
            second.LastTexture();
            now += SlideTextureCache.MIN_FETCH_INTERVAL_SECONDS;
            SetMetadata(otherBot, V2("null", "idle", currentSlide: 1, slideUrl: SlideUrl(3)));
            second.EnsureVideoIsPlaying();
            second.LastTexture();
            SetMetadata(bot, V2("null", "idle", currentSlide: 1, slideUrl: SlideUrl(4)));
            first.EnsureVideoIsPlaying();
            first.LastTexture();

            // Act
            slideCache.ClearThrottled(10);

            // Assert
            Assert.IsTrue(shown != null);
        }

        private static string SlideUrl(int index) =>
            $"{ALLOWED_SLIDES}{index:x16}.png";

        private void ComposeThenReconnect()
        {
            LKParticipant bot = AddParticipant(BOT, V2_METADATA);
            Subscribe(bot, AddTrack(bot, "TR_pv", TrackKind.KindVideo, TrackSource.SourceScreenshare, LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME));
            LKParticipant presenter = AddParticipant(PRESENTER);
            Subscribe(presenter, AddTrack(presenter, "TR_cam", TrackKind.KindVideo, TrackSource.SourceCamera));
            LivekitPlayer p = NewPlayer();
            p.OpenMedia(LivekitAddress.CurrentStream());
            p.EnsureVideoIsPlaying();

            room.ConnectionUpdated += Raise.Event<ConnectionDelegate>(room, ConnectionUpdate.Reconnected, null);
            p.EnsureVideoIsPlaying();
        }

        private static string V2(string playingVideoIndex, string videoState, int currentSlide = 0, string presenter = PRESENTER, int width = 1920, int height = 1080, string slideUrl = SLIDE_URL) =>
            $"{{\"role\":\"presentation\",\"currentSlide\":{currentSlide},\"slide\":{{\"url\":\"{slideUrl}\",\"width\":{width},\"height\":{height}}},\"presenterIdentity\":\"{presenter}\","
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
            LKParticipant participant = LiveKitTestObjects.NewParticipant(identity, metadata);

            remoteParticipants[identity] = participant;
            participantsHub.RemoteParticipant(identity).Returns(participant);
            return participant;
        }

        private void RemoveParticipant(LKParticipant participant)
        {
            string identity = participant.Identity;
            remoteParticipants.Remove(identity);
            participantsHub.RemoteParticipant(identity).Returns((LKParticipant?)null);
            participantsHub.UpdatesFromParticipant += Raise.Event<ParticipantDelegate>(participant, UpdateFromParticipant.Disconnected);
        }

        private void SetMetadata(LKParticipant participant, string metadata)
        {
            participant.UpdateMeta(metadata);
            participantsHub.UpdatesFromParticipant += Raise.Event<ParticipantDelegate>(participant, UpdateFromParticipant.MetadataChanged);
        }

        private static TrackPublication AddTrack(LKParticipant participant, string sid, TrackKind kind, TrackSource source, string name = "", bool muted = false)
        {
            TrackPublication track = LiveKitTestObjects.NewPublication(sid, kind, source, name, muted);
            participant.AddTrack(track);
            return track;
        }

        private IVideoStream Subscribe(LKParticipant participant, TrackPublication track)
        {
            IVideoStream stream = Substitute.For<IVideoStream>();
            resolvedStreams[new StreamKey(participant.Identity, track.Sid)] = new Owned<IVideoStream>(stream).Downgrade();
            room.TrackSubscribed += Raise.Event<SubscribeDelegate>(Substitute.For<ITrack>(), track, participant);
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

        private LivekitPlayer NewPlayer() =>
            NewPlayer(NewSlideCache());

        private LivekitPlayer NewPlayer(SlideTextureCache slideCache)
        {
            var material = new Material(Shader.Find("DCL/PresentationCompositor"));
            created.Add(material);
            var p = new LivekitPlayer(room, () => true, null, slideCache, material);
            players.Add(p);
            return p;
        }

        private SlideTextureCache NewSlideCacheHolding(IWebRequestController controller, string url, Func<float> getRealtimeSinceStartup)
        {
            var slideCache = new SlideTextureCache(controller, decentralandUrlsSource, getRealtimeSinceStartup);
            slideCaches.Add(slideCache);
            SlideRequest(controller).ReturnsForAnyArgs(UniTask.FromResult<Texture2D?>(new Texture2D(2, 2)), new UniTaskCompletionSource<Texture2D?>().Task);
            slideCache.BeginComposing();
            slideCache.GetOrRequest(url, BOT);
            return slideCache;
        }

        private static UniTask<Texture2D?> SlideRequest(IWebRequestController controller) =>
            controller.SendAsync<GetTextureWebRequest, GetTextureArguments, SlideTextureCache.SlideTextureOp, Texture2D>(default, default);

        private SlideTextureCache NewSlideCache()
        {
            var slideCache = new SlideTextureCache(Substitute.For<IWebRequestController>(), decentralandUrlsSource);
            slideCaches.Add(slideCache);
            return slideCache;
        }
    }
}
