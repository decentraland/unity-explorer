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
using System.Collections.Concurrent;
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
        private IParticipantsHub participantsHub = null!;
        private FakeActiveSpeakers activeSpeakers = null!;
        private NearbyAudioStreamRegistry registry = null!;

        [SetUp]
        public void SetUp()
        {
            room = Substitute.For<IRoom>();
            participantsHub = Substitute.For<IParticipantsHub>();
            room.Participants.Returns(participantsHub);
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
        public void IgnoreScreenshareAudioOnSubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio, TrackSource.SourceScreenshareAudio);

            Assert.That(registry.GetAudioSids(WALLET_A), Is.Null);
        }

        [Test]
        public void IgnoreUnknownSourceAudioOnSubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio, TrackSource.SourceUnknown);

            Assert.That(registry.GetAudioSids(WALLET_A), Is.Null);
        }

        [Test]
        public void IgnoreScreenshareAudioOnRehydrate()
        {
            SetupRemoteParticipants(
                (WALLET_A, new[]
                {
                    (SID_1, TrackKind.KindAudio, TrackSource.SourceMicrophone),
                    (SID_2, TrackKind.KindAudio, TrackSource.SourceScreenshareAudio),
                }));

            RaiseConnectionUpdated(ConnectionUpdate.Connected);

            var sids = registry.GetAudioSids(WALLET_A);
            Assert.That(sids, Is.Not.Null);
            Assert.That(sids!.ContainsKey(SID_1), Is.True);
            Assert.That(sids.ContainsKey(SID_2), Is.False);
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
        public void RemoveSidWhenAudioTrackUnpublished()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseTrackUnpublished(WALLET_A, SID_1, TrackKind.KindAudio);

            Assert.That(registry.GetAudioSids(WALLET_A), Is.Null);
        }

        [Test]
        public void IgnoreNullPublicationOnUnpublished()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            LKParticipant participant = NewParticipant(WALLET_A);
            Assert.DoesNotThrow(() =>
                room.TrackUnpublished += Raise.Event<PublishDelegate>(null!, participant));

            var sids = registry.GetAudioSids(WALLET_A);
            Assert.That(sids, Is.Not.Null);
            Assert.That(sids!.ContainsKey(SID_1), Is.True);
        }

        [Test]
        public void IgnoreNonAudioTrackUnpublished()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseTrackUnpublished(WALLET_A, SID_VIDEO, TrackKind.KindVideo);

            var sids = registry.GetAudioSids(WALLET_A);
            Assert.That(sids, Is.Not.Null);
            Assert.That(sids!.ContainsKey(SID_1), Is.True);
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
        public void DropAllSidsWhenParticipantDisconnects()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio);
            RaiseTrackSubscribed(WALLET_B, SID_VIDEO, TrackKind.KindAudio);

            RaiseParticipantUpdated(WALLET_A, UpdateFromParticipant.Disconnected);

            Assert.That(registry.GetAudioSids(WALLET_A), Is.Null);
            Assert.That(registry.GetAudioSids(WALLET_B), Is.Not.Null);
            Assert.That(registry.GetAudioSids(WALLET_B)!.ContainsKey(SID_VIDEO), Is.True);
        }

        [Test]
        public void IgnoreNonDisconnectedParticipantUpdate()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseParticipantUpdated(WALLET_A, UpdateFromParticipant.MetadataChanged);

            var sids = registry.GetAudioSids(WALLET_A);
            Assert.That(sids, Is.Not.Null);
            Assert.That(sids!.ContainsKey(SID_1), Is.True);
        }

        [Test]
        public void RehydrateOnReconnected()
        {
            SetupRemoteParticipants((WALLET_A, new[] { (SID_1, TrackKind.KindAudio) }));
            RaiseConnectionUpdated(ConnectionUpdate.Connected);

            SetupRemoteParticipants((WALLET_B, new[] { (SID_2, TrackKind.KindAudio) }));
            RaiseConnectionUpdated(ConnectionUpdate.Reconnected);

            Assert.That(registry.GetAudioSids(WALLET_A), Is.Null);
            Assert.That(registry.GetAudioSids(WALLET_B), Is.Not.Null);
            Assert.That(registry.GetAudioSids(WALLET_B)!.ContainsKey(SID_2), Is.True);
        }

        [Test]
        public void PullActiveSpeakersOnReconnected()
        {
            activeSpeakers.SetActives(WALLET_A);
            Assert.That(registry.IsActiveSpeaker(WALLET_A), Is.True);

            activeSpeakers.SetSilently(WALLET_B);

            RaiseConnectionUpdated(ConnectionUpdate.Reconnected);

            Assert.That(registry.IsActiveSpeaker(WALLET_A), Is.False);
            Assert.That(registry.IsActiveSpeaker(WALLET_B), Is.True);
        }

        [Test]
        public void PullActiveSpeakersOnConnected()
        {
            activeSpeakers.SetActives(WALLET_A);
            Assert.That(registry.IsActiveSpeaker(WALLET_A), Is.True);

            activeSpeakers.SetSilently(WALLET_B);

            RaiseConnectionUpdated(ConnectionUpdate.Connected);

            Assert.That(registry.IsActiveSpeaker(WALLET_A), Is.False);
            Assert.That(registry.IsActiveSpeaker(WALLET_B), Is.True);
        }

        [Test]
        public void IgnoreReconnectingUpdate()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseConnectionUpdated(ConnectionUpdate.Reconnecting);

            var sids = registry.GetAudioSids(WALLET_A);
            Assert.That(sids, Is.Not.Null);
            Assert.That(sids!.ContainsKey(SID_1), Is.True);
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

        private void RaiseTrackSubscribed(string identity, string sid, TrackKind kind, TrackSource source = TrackSource.SourceMicrophone)
        {
            LKParticipant participant = NewParticipant(identity);
            TrackPublication publication = NewPublication(sid, kind, source);
            room.TrackSubscribed += Raise.Event<SubscribeDelegate>(null!, publication, participant);
        }

        private void RaiseTrackUnsubscribed(string identity, string sid)
        {
            LKParticipant participant = NewParticipant(identity);
            TrackPublication publication = NewPublication(sid, TrackKind.KindAudio);
            room.TrackUnsubscribed += Raise.Event<SubscribeDelegate>(null!, publication, participant);
        }

        private void RaiseTrackUnpublished(string identity, string sid, TrackKind kind)
        {
            LKParticipant participant = NewParticipant(identity);
            TrackPublication publication = NewPublication(sid, kind);
            room.TrackUnpublished += Raise.Event<PublishDelegate>(publication, participant);
        }

        private void RaiseConnectionUpdated(ConnectionUpdate update)
        {
            room.ConnectionUpdated += Raise.Event<ConnectionDelegate>(room, update, (LKDisconnectReason?)null);
        }

        private void RaiseParticipantUpdated(string identity, UpdateFromParticipant update)
        {
            LKParticipant participant = NewParticipant(identity);
            participantsHub.UpdatesFromParticipant += Raise.Event<ParticipantDelegate>(participant, update);
        }

        private void SetupRemoteParticipants(params (string identity, (string sid, TrackKind kind)[] tracks)[] participants)
        {
            var dict = new Dictionary<string, LKParticipant>();
            foreach ((string identity, (string sid, TrackKind kind)[] tracks) in participants)
            {
                var triples = new (string sid, TrackKind kind, TrackSource source)[tracks.Length];
                for (int i = 0; i < tracks.Length; i++)
                    triples[i] = (tracks[i].sid, tracks[i].kind, TrackSource.SourceMicrophone);
                dict[identity] = NewParticipantWithTracks(identity, triples);
            }

            participantsHub.RemoteParticipantIdentities().Returns(dict);
        }

        private void SetupRemoteParticipants(params (string identity, (string sid, TrackKind kind, TrackSource source)[] tracks)[] participants)
        {
            var dict = new Dictionary<string, LKParticipant>();
            foreach ((string identity, (string sid, TrackKind kind, TrackSource source)[] tracks) in participants)
                dict[identity] = NewParticipantWithTracks(identity, tracks);

            participantsHub.RemoteParticipantIdentities().Returns(dict);
        }

        private static LKParticipant NewParticipantWithTracks(string identity, (string sid, TrackKind kind, TrackSource source)[] tracks)
        {
            LKParticipant participant = NewParticipant(identity);

            FieldInfo tracksField = typeof(LKParticipant).GetField("tracks", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var tracksDict = (ConcurrentDictionary<string, TrackPublication>)tracksField.GetValue(participant)!;
            foreach ((string sid, TrackKind kind, TrackSource source) in tracks)
                tracksDict[sid] = NewPublication(sid, kind, source);

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

        private static TrackPublication NewPublication(string sid, TrackKind kind, TrackSource source = TrackSource.SourceMicrophone)
        {
            var info = new TrackPublicationInfo { Sid = sid, Kind = kind, Source = source };
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

            public void SetSilently(params string[] ids)
            {
                set.Clear();
                foreach (string id in ids) set.Add(id);
            }
        }
    }
}
