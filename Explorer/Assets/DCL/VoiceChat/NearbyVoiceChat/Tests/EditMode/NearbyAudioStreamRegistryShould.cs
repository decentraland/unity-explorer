using DCL.LiveKit.Public;
using DCL.VoiceChat.Nearby.Audio;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
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

            Assert.That(registry.HasAudioStream(WALLET_A), Is.True);
            Assert.That(ContainsSid(WALLET_A, SID_1), Is.True);
        }

        [Test]
        public void IgnoreSidWhenNonAudioTrackSubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindVideo);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
            Assert.That(registry.GetAudioSidsArray(WALLET_A), Is.Null);
        }

        [Test]
        public void RemoveSidWhenTrackUnsubscribedAndPreserveIdentityIfOthersRemain()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio);

            RaiseTrackUnsubscribed(WALLET_A, SID_1);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.True);
            Assert.That(ContainsSid(WALLET_A, SID_1), Is.False);
            Assert.That(ContainsSid(WALLET_A, SID_2), Is.True);
        }

        [Test]
        public void RemoveParticipantEntryWhenLastSidUnsubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseTrackUnsubscribed(WALLET_A, SID_1);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
            Assert.That(registry.GetAudioSidsArray(WALLET_A), Is.Null);
        }

        [Test]
        public void ReturnNullForUnknownWallet()
        {
            Assert.That(registry.HasAudioStream("0xUNKNOWN"), Is.False);
            Assert.That(registry.GetAudioSidsArray("0xUNKNOWN"), Is.Null);
            Assert.That(registry.GetAudioSids("0xUNKNOWN").Length, Is.EqualTo(0));
        }

        [Test]
        public void SeedAudioSidsForExistingParticipantsOnConnected()
        {
            SetupRemoteParticipants(
                (WALLET_A, new[] { (SID_1, TrackKind.KindAudio), (SID_VIDEO, TrackKind.KindVideo) }),
                (WALLET_B, new[] { (SID_2, TrackKind.KindAudio) }));

            RaiseConnectionUpdated(ConnectionUpdate.Connected);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.True);
            Assert.That(ContainsSid(WALLET_A, SID_1), Is.True);
            Assert.That(ContainsSid(WALLET_A, SID_VIDEO), Is.False);

            Assert.That(registry.HasAudioStream(WALLET_B), Is.True);
            Assert.That(ContainsSid(WALLET_B, SID_2), Is.True);
        }

        [Test]
        public void ClearPriorStateOnSecondConnected()
        {
            SetupRemoteParticipants((WALLET_A, new[] { (SID_1, TrackKind.KindAudio) }));
            RaiseConnectionUpdated(ConnectionUpdate.Connected);

            SetupRemoteParticipants((WALLET_B, new[] { (SID_2, TrackKind.KindAudio) }));
            RaiseConnectionUpdated(ConnectionUpdate.Connected);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
            Assert.That(registry.HasAudioStream(WALLET_B), Is.True);
        }

        [Test]
        public void ClearRegistryOnDisconnected()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseConnectionUpdated(ConnectionUpdate.Disconnected);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
        }

        [Test]
        public void UnsubscribeAndClearOnDispose()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            Assert.That(registry.HasAudioStream(WALLET_A), Is.True);

            registry.Dispose();

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);

            RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
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
            Assert.That(registry.HasAudioStream(WALLET_B), Is.False);
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
                    // Snapshot the COW reference; iterate it freely without further locking.
                    string[]? snapshot = registry.GetAudioSidsArray(WALLET_A);
                    if (snapshot != null)
                        for (int s = 0; s < snapshot.Length; s++)
                            _ = snapshot[s];
                }
            });

            Assert.DoesNotThrow(() => Task.WaitAll(new[] { writer, reader }, millisecondsTimeout: 5000));
        }

        // ── COW reference-equality contract (B2.1) ──────────────────

        [Test]
        public void SubscribingThenUnsubscribingSameSidProducesDistinctArrayReferences()
        {
            RaiseTrackSubscribed(WALLET_A, "sid-A", TrackKind.KindAudio);
            string[] ref1 = registry.GetAudioSidsArray(WALLET_A)!;

            RaiseTrackSubscribed(WALLET_A, "sid-B", TrackKind.KindAudio);
            string[] ref2 = registry.GetAudioSidsArray(WALLET_A)!;

            RaiseTrackUnsubscribed(WALLET_A, "sid-A");
            string[] ref3 = registry.GetAudioSidsArray(WALLET_A)!;

            Assert.That(ref1, Is.Not.Null);
            Assert.That(ref2, Is.Not.Null);
            Assert.That(ref3, Is.Not.Null);
            Assert.That(ReferenceEquals(ref1, ref2), Is.False, "add must produce a new reference");
            Assert.That(ReferenceEquals(ref2, ref3), Is.False, "remove must produce a new reference");
        }

        [Test]
        public void SameSidSetObservedTwiceReturnsSameReference()
        {
            RaiseTrackSubscribed(WALLET_A, "sid-A", TrackKind.KindAudio);

            string[]? ref1 = registry.GetAudioSidsArray(WALLET_A);
            string[]? ref2 = registry.GetAudioSidsArray(WALLET_A);

            Assert.That(ref1, Is.Not.Null);
            Assert.That(ReferenceEquals(ref1, ref2), Is.True,
                "stable observation window must yield reference-stable snapshot — this is the freshness signal Bridge consumes");
        }

        [Test]
        public void IsStreamGoneReturnsTrueAfterLastSidUnsubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            Assert.That(registry.IsStreamGone(new StreamKey(WALLET_A, SID_1)), Is.False);

            RaiseTrackUnsubscribed(WALLET_A, SID_1);

            Assert.That(registry.IsStreamGone(new StreamKey(WALLET_A, SID_1)), Is.True,
                "wallet must be evicted entirely when the last sid is unsubscribed");
            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
        }

        [Test]
        public void ConcurrentSubUnsubUnderContentionDoesNotLoseUpdates()
        {
            // Stress the COW factory's CAS-retry semantics. After all events are applied the
            // observable end state is deterministic by composition (sub sid-A, sub sid-B, unsub sid-A).
            const int ROUNDS = 200;

            for (int r = 0; r < ROUNDS; r++)
            {
                Task t1 = Task.Run(() => RaiseTrackSubscribed(WALLET_A, "sid-A", TrackKind.KindAudio));
                Task t2 = Task.Run(() => RaiseTrackSubscribed(WALLET_A, "sid-B", TrackKind.KindAudio));
                Task.WaitAll(t1, t2);

                Task t3 = Task.Run(() => RaiseTrackUnsubscribed(WALLET_A, "sid-A"));
                t3.Wait();

                Assert.That(ContainsSid(WALLET_A, "sid-B"), Is.True, "sid-B must survive every round");
                Assert.That(ContainsSid(WALLET_A, "sid-A"), Is.False, "sid-A must be gone every round");

                // Reset for next round.
                RaiseTrackUnsubscribed(WALLET_A, "sid-B");
                Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
            }
        }

        // ── Helpers ─────────────────────────────────────────────────

        private bool ContainsSid(string walletId, string sid)
        {
            string[]? arr = registry.GetAudioSidsArray(walletId);
            if (arr == null) return false;
            return Array.IndexOf(arr, sid) >= 0;
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
            var tracksDict = (ConcurrentDictionary<string, TrackPublication>)tracksField.GetValue(participant)!;
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
