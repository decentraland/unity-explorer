using DCL.SDKComponents.MediaStream;
using LiveKit.Internal;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using LiveKit.Rooms.VideoStreaming;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DCL.SDKComponents.MediaStream.Tests
{
    // Regression (UNITY-EXPLORER-NQB): ActiveStream on a not-yet-subscribed track NRE'd out of UpdateMediaPlayerSystem.Update.
    public class LivekitPlayerShould
    {
        private const string IDENTITY = "peer-1";
        private const string SID = "TR_video0";

        private IRoom room = null!;
        private IParticipantsHub participants = null!;
        private IVideoStreams videoStreams = null!;
        private IAudioStreams audioStreams = null!;

        [SetUp]
        public void SetUp()
        {
            room = Substitute.For<IRoom>();
            participants = Substitute.For<IParticipantsHub>();
            videoStreams = Substitute.For<IVideoStreams>();
            audioStreams = Substitute.For<IAudioStreams>();

            room.Participants.Returns(participants);
            room.VideoStreams.Returns(videoStreams);
            room.AudioStreams.Returns(audioStreams);

            participants.RemoteParticipantIdentities().Returns(new Dictionary<string, LKParticipant>());
        }

        [Test]
        public void NotThrowWhenPublishedVideoTrackIsNotYetSubscribed()
        {
            participants.RemoteParticipant(IDENTITY).Returns(ParticipantWith(SID, subscribedTrack: null));

            videoStreams.When(v => v.ActiveStream(Arg.Any<StreamKey>()))
                        .Do(_ => throw new NullReferenceException("track.Handle!.DangerousGetHandle()"));

            var player = new LivekitPlayer(room, null, null);
            LivekitAddress address = LivekitAddress.FromUserStream(new UserStream(IDENTITY, SID));

            Assert.DoesNotThrow(() => player.OpenMedia(address));
            videoStreams.DidNotReceive().ActiveStream(Arg.Any<StreamKey>());

            player.Dispose();
        }

        [Test]
        public void OpenVideoStreamWhenSubscribedTrackHandleIsValid()
        {
            var track = Substitute.For<ITrack>();
            track.Handle.Returns(ValidHandle());

            participants.RemoteParticipant(IDENTITY).Returns(ParticipantWith(SID, track));

            var player = new LivekitPlayer(room, null, null);
            LivekitAddress address = LivekitAddress.FromUserStream(new UserStream(IDENTITY, SID));

            player.OpenMedia(address);

            videoStreams.Received(1).ActiveStream(new StreamKey(IDENTITY, SID));

            player.Dispose();
        }

        // A null `subscribedTrack` reproduces the "visible but not yet subscribed" state (Track == null).
        private static LKParticipant ParticipantWith(string sid, ITrack? subscribedTrack)
        {
            var participant = new LKParticipant();
            var publication = new TrackPublication();

            if (subscribedTrack != null)
            {
                MethodInfo updateTrack = typeof(TrackPublication)
                    .GetMethod("UpdateTrack", BindingFlags.Instance | BindingFlags.NonPublic)!;
                updateTrack.Invoke(publication, new object[] { subscribedTrack });
            }

            FieldInfo tracksField = typeof(LKParticipant)
                .GetField("tracks", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var tracks = (IDictionary<string, TrackPublication>)tracksField.GetValue(participant)!;
            tracks[sid] = publication;

            return participant;
        }

        // FfiHandle exposes no public way to a valid handle (internal Construct); poke the field so
        // IsInvalid == false (non-zero, non -1).
        private static FfiHandle ValidHandle()
        {
            var handle = new FfiHandle();
            FieldInfo field = typeof(FfiHandle)
                .GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(handle, new IntPtr(0x1BADF00D));
            return handle;
        }
    }
}
