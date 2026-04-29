using DCL.LiveKit.Public;
using DCL.VoiceChat.Nearby.Audio;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks.Hub;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.VoiceChat.Nearby.Tests
{
    [TestFixture]
    public class NearbyAudioStreamRegistryShould
    {
        private const string WALLET_A = "0xAAA";
        private const string WALLET_B = "0xBBB";
        private const string SID_1 = "TR_track-1";
        private const string SID_2 = "TR_track-2";
        private const string SID_VIDEO = "TR_video-1";

        private IRoom room = null!;
        private FakeActiveSpeakers activeSpeakers = null!;
        private NearbyAudioStreamRegistry registry = null!;

        [SetUp]
        public void SetUp()
        {
            room = Substitute.For<IRoom>();
            activeSpeakers = new FakeActiveSpeakers();
            room.ActiveSpeakers.Returns(activeSpeakers);
            registry = new NearbyAudioStreamRegistry(room);
        }

        [TearDown]
        public void TearDown()
        {
            registry.Dispose();
        }

        [Test]
        public void AddSidWhenAudioTrackSubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            var sids = registry.GetAudioSids(WALLET_A);

            Assert.That(sids, Is.Not.Null);
            Assert.That(sids!.ContainsKey(SID_1), Is.True);
        }

        [Test]
        public void IgnoreSidWhenNonAudioTrackSubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindVideo);

            Assert.That(registry.GetAudioSids(WALLET_A), Is.Null);
        }

        [Test]
        public void RemoveSidWhenTrackUnsubscribedAndPreserveIdentityIfOthersRemain()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio);

            RaiseTrackUnsubscribed(WALLET_A, SID_1);

            var sids = registry.GetAudioSids(WALLET_A);
            Assert.That(sids, Is.Not.Null);
            Assert.That(sids!.ContainsKey(SID_1), Is.False);
            Assert.That(sids.ContainsKey(SID_2), Is.True);
        }

        [Test]
        public void RemoveParticipantEntryWhenLastSidUnsubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseTrackUnsubscribed(WALLET_A, SID_1);

            Assert.That(registry.GetAudioSids(WALLET_A), Is.Null);
        }

        [Test]
        public void ReturnNullForUnknownWallet()
        {
            Assert.That(registry.GetAudioSids("0xUNKNOWN"), Is.Null);
        }

        [Test]
        public void SeedAudioSidsForExistingParticipantsOnConnected()
        {
            SetupRemoteParticipants(
                (WALLET_A, new[] { (SID_1, TrackKind.KindAudio), (SID_VIDEO, TrackKind.KindVideo) }),
                (WALLET_B, new[] { (SID_2, TrackKind.KindAudio) }));

            RaiseConnectionUpdated(ConnectionUpdate.Connected);

            var sidsA = registry.GetAudioSids(WALLET_A);
            var sidsB = registry.GetAudioSids(WALLET_B);

            Assert.That(sidsA, Is.Not.Null);
            Assert.That(sidsA!.ContainsKey(SID_1), Is.True);
            Assert.That(sidsA.ContainsKey(SID_VIDEO), Is.False);
            Assert.That(sidsB, Is.Not.Null);
            Assert.That(sidsB!.ContainsKey(SID_2), Is.True);
        }

        [Test]
        public void ClearPriorStateOnSecondConnected()
        {
            SetupRemoteParticipants((WALLET_A, new[] { (SID_1, TrackKind.KindAudio) }));
            RaiseConnectionUpdated(ConnectionUpdate.Connected);

            SetupRemoteParticipants((WALLET_B, new[] { (SID_2, TrackKind.KindAudio) }));
            RaiseConnectionUpdated(ConnectionUpdate.Connected);

            Assert.That(registry.GetAudioSids(WALLET_A), Is.Null);
            Assert.That(registry.GetAudioSids(WALLET_B), Is.Not.Null);
        }

        [Test]
        public void ClearRegistryOnDisconnected()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseConnectionUpdated(ConnectionUpdate.Disconnected);

            Assert.That(registry.GetAudioSids(WALLET_A), Is.Null);
        }

        [Test]
        public void UnsubscribeAndClearOnDispose()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            Assert.That(registry.GetAudioSids(WALLET_A), Is.Not.Null);

            registry.Dispose();

            Assert.That(registry.GetAudioSids(WALLET_A), Is.Null);

            RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio);

            Assert.That(registry.GetAudioSids(WALLET_A), Is.Null);
        }

        [Test]
        public void TrackWalletAsActiveSpeakerWhenUpdatedRaisedWithIt()
        {
            activeSpeakers.SetActives(WALLET_A);

            Assert.That(registry.IsActiveSpeaker(WALLET_A), Is.True);
        }

        [Test]
        public void DropWalletWhenUpdatedRaisedWithoutIt()
        {
            activeSpeakers.SetActives(WALLET_A);
            activeSpeakers.SetActives(/* empty */);

            Assert.That(registry.IsActiveSpeaker(WALLET_A), Is.False);
        }

        [Test]
        public void ClearActiveSpeakersOnDisconnected()
        {
            activeSpeakers.SetActives(WALLET_A);

            RaiseConnectionUpdated(ConnectionUpdate.Disconnected);

            Assert.That(registry.IsActiveSpeaker(WALLET_A), Is.False);
        }

        [Test]
        public void IsolateActiveSpeakersFromAudioSidsIndex()
        {
            // Track subscribe must NOT auto-mark the wallet as an active speaker.
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            Assert.That(registry.IsActiveSpeaker(WALLET_A), Is.False);

            // ActiveSpeakers update must NOT auto-add to the audio-sids index.
            activeSpeakers.SetActives(WALLET_B);
            Assert.That(registry.GetAudioSids(WALLET_B), Is.Null);
        }

        [Test]
        public void NotThrowWhenMutationsAreInterleavedWithReadsFromMultipleThreads()
        {
            const int ITERATIONS = 500;
            using var cts = new CancellationTokenSource();

            Task writer = Task.Run(() =>
            {
                for (int i = 0; i < ITERATIONS && !cts.IsCancellationRequested; i++)
                {
                    string sid = "TR_" + i;
                    RaiseTrackSubscribed(WALLET_A, sid, TrackKind.KindAudio);
                    RaiseTrackUnsubscribed(WALLET_A, sid);
                }
            });

            Task reader = Task.Run(() =>
            {
                for (int i = 0; i < ITERATIONS && !cts.IsCancellationRequested; i++)
                {
                    var sids = registry.GetAudioSids(WALLET_A);
                    if (sids != null)
                        foreach (var _ in sids) { /* drain enumerator to surface concurrency issues */ }
                }
            });

            Assert.DoesNotThrow(() => Task.WaitAll(new[] { writer, reader }, millisecondsTimeout: 5000));
        }

        private void RaiseTrackSubscribed(string identity, string sid, TrackKind kind)
        {
            LKParticipant participant = NewParticipant(identity);
            TrackPublication publication = NewPublication(sid, kind);
            room.TrackSubscribed += Raise.Event<SubscribeDelegate>(null!, publication, participant);
        }

        private void RaiseTrackUnsubscribed(string identity, string sid)
        {
            LKParticipant participant = NewParticipant(identity);
            TrackPublication publication = NewPublication(sid, TrackKind.KindAudio);
            room.TrackUnsubscribed += Raise.Event<SubscribeDelegate>(null!, publication, participant);
        }

        private void RaiseConnectionUpdated(ConnectionUpdate update)
        {
            room.ConnectionUpdated += Raise.Event<ConnectionDelegate>(room, update, (LKDisconnectReason?)null);
        }

        private void SetupRemoteParticipants(params (string identity, (string sid, TrackKind kind)[] tracks)[] participants)
        {
            var dict = new Dictionary<string, LKParticipant>();
            foreach ((string identity, (string sid, TrackKind kind)[] tracks) in participants)
                dict[identity] = NewParticipantWithTracks(identity, tracks);

            var hub = Substitute.For<IParticipantsHub>();
            hub.RemoteParticipantIdentities().Returns(dict);
            room.Participants.Returns(hub);
        }

        private static LKParticipant NewParticipantWithTracks(string identity, (string sid, TrackKind kind)[] tracks)
        {
            LKParticipant participant = NewParticipant(identity);

            FieldInfo tracksField = typeof(LKParticipant).GetField("tracks", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var tracksDict = (Dictionary<string, TrackPublication>)tracksField.GetValue(participant)!;
            foreach ((string sid, TrackKind kind) in tracks)
                tracksDict[sid] = NewPublication(sid, kind);

            return participant;
        }

        private static LKParticipant NewParticipant(string identity)
        {
            var info = new ParticipantInfo { Identity = identity };
            var participant = new LKParticipant();
            typeof(LKParticipant).GetField("info", BindingFlags.Instance | BindingFlags.NonPublic)!
                                 .SetValue(participant, info);
            return participant;
        }

        private static TrackPublication NewPublication(string sid, TrackKind kind)
        {
            var info = new TrackPublicationInfo { Sid = sid, Kind = kind };
            var publication = new TrackPublication();
            typeof(TrackPublication).GetField("info", BindingFlags.Instance | BindingFlags.NonPublic)!
                                    .SetValue(publication, info);
            return publication;
        }

        private sealed class FakeActiveSpeakers : IActiveSpeakers
        {
            private readonly HashSet<string> set = new ();
            public event Action Updated = delegate { };

            public int Count => set.Count;
            public IEnumerator<string> GetEnumerator() => set.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            public void SetActives(params string[] ids)
            {
                set.Clear();
                foreach (string id in ids) set.Add(id);
                Updated.Invoke();
            }
        }
    }
}
