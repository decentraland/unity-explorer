using DCL.LiveKit.Public;
using DCL.VoiceChat.Nearby.Audio;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
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
        private IParticipantsHub participantsHub = null!;
        private FakeActiveSpeakers activeSpeakers = null!;
        private IAudioStreams audioStreams = null!;
        private NearbyAudioStreamsRegistry registry = null!;

        [SetUp]
        public void SetUp()
        {
            room = Substitute.For<IRoom>();
            participantsHub = Substitute.For<IParticipantsHub>();
            room.Participants.Returns(participantsHub);
            activeSpeakers = new FakeActiveSpeakers();
            room.ActiveSpeakers.Returns(activeSpeakers);
            audioStreams = Substitute.For<IAudioStreams>();
            // -1 sentinel matches production: missing stream / never decoded a frame.
            audioStreams.GetLastFrameReceivedAt(default).ReturnsForAnyArgs(-1);
            audioStreams.ClearReceivedCalls(); // stub-setup call counts as received; clear so DidNotReceive assertions are clean.
            room.AudioStreams.Returns(audioStreams);
            registry = new NearbyAudioStreamsRegistry(room);
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
        public void IgnoreScreenshareAudioOnSubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio, TrackSource.SourceScreenshareAudio);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
        }

        [Test]
        public void IgnoreUnknownSourceAudioOnSubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio, TrackSource.SourceUnknown);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
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

            Assert.That(registry.HasAudioStream(WALLET_A), Is.True);
            Assert.That(ContainsSid(WALLET_A, SID_1), Is.True);
            Assert.That(ContainsSid(WALLET_A, SID_2), Is.False);
        }

        [Test]
        public void IgnoreSidWhenNonAudioTrackSubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindVideo);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
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
        public void RemoveSidWhenAudioTrackUnpublished()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseTrackUnpublished(WALLET_A, SID_1, TrackKind.KindAudio);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
        }

        [Test]
        public void IgnoreNullPublicationOnUnpublished()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            LKParticipant participant = NewParticipant(WALLET_A);
            Assert.DoesNotThrow(() =>
                room.TrackUnpublished += Raise.Event<PublishDelegate>(null!, participant));

            Assert.That(registry.HasAudioStream(WALLET_A), Is.True);
            Assert.That(ContainsSid(WALLET_A, SID_1), Is.True);
        }

        [Test]
        public void IgnoreNonAudioTrackUnpublished()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseTrackUnpublished(WALLET_A, SID_VIDEO, TrackKind.KindVideo);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.True);
            Assert.That(ContainsSid(WALLET_A, SID_1), Is.True);
        }

        [Test]
        public void RemoveParticipantEntryWhenLastSidUnsubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseTrackUnsubscribed(WALLET_A, SID_1);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
            Assert.That(registry.GetActiveSid(WALLET_A), Is.Null);
        }

        [Test]
        public void ReturnNullForUnknownWallet()
        {
            Assert.That(registry.HasAudioStream("0xUNKNOWN"), Is.False);
            Assert.That(registry.GetActiveSid("0xUNKNOWN"), Is.Null);
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
        public void DropAllSidsWhenParticipantDisconnects()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio);
            RaiseTrackSubscribed(WALLET_B, SID_VIDEO, TrackKind.KindAudio);

            RaiseParticipantUpdated(WALLET_A, UpdateFromParticipant.Disconnected);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
            Assert.That(registry.HasAudioStream(WALLET_B), Is.True);
            Assert.That(ContainsSid(WALLET_B, SID_VIDEO), Is.True);
        }

        [Test]
        public void IgnoreNonDisconnectedParticipantUpdate()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            RaiseParticipantUpdated(WALLET_A, UpdateFromParticipant.MetadataChanged);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.True);
            Assert.That(ContainsSid(WALLET_A, SID_1), Is.True);
        }

        [Test]
        public void RehydrateOnReconnected()
        {
            SetupRemoteParticipants((WALLET_A, new[] { (SID_1, TrackKind.KindAudio) }));
            RaiseConnectionUpdated(ConnectionUpdate.Connected);

            SetupRemoteParticipants((WALLET_B, new[] { (SID_2, TrackKind.KindAudio) }));
            RaiseConnectionUpdated(ConnectionUpdate.Reconnected);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
            Assert.That(registry.HasAudioStream(WALLET_B), Is.True);
            Assert.That(ContainsSid(WALLET_B, SID_2), Is.True);
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

            Assert.That(registry.HasAudioStream(WALLET_A), Is.True);
            Assert.That(ContainsSid(WALLET_A, SID_1), Is.True);
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
        public void ReaderNeverObservesPartialSnapshotDuringConcurrentReconnect()
        {
            // Atomic-publish invariant: a main-thread reader polling HasAudioStream for an identity
            // that is present both before and after rehydrate must never observe a transient false
            // mid-rehydrate. With in-place Clear()+rebuild the reader would see a "wallet missing"
            // window; with Interlocked.Exchange-based snapshot swap it only sees pre- or post-state.
            SetupRemoteParticipants((WALLET_A, new[] { (SID_1, TrackKind.KindAudio) }));
            RaiseConnectionUpdated(ConnectionUpdate.Connected);

            const int RECONNECT_ITERATIONS = 500;
            using var cts = new CancellationTokenSource();
            string? observedMissingAt = null;

            Task reader = Task.Run(() =>
            {
                int loops = 0;
                while (!cts.IsCancellationRequested)
                {
                    if (!registry.HasAudioStream(WALLET_A))
                    {
                        observedMissingAt = "loop " + loops;
                        return;
                    }
                    loops++;
                }
            });

            Task writer = Task.Run(() =>
            {
                for (int i = 0; i < RECONNECT_ITERATIONS; i++)
                    RaiseConnectionUpdated(ConnectionUpdate.Reconnected);
            });

            Assert.That(writer.Wait(millisecondsTimeout: 5000), Is.True, "writer must finish within timeout");
            cts.Cancel();
            Assert.That(reader.Wait(millisecondsTimeout: 5000), Is.True, "reader must terminate within timeout");

            Assert.That(observedMissingAt, Is.Null,
                $"WALLET_A is present pre- and post-rehydrate; reader observed transient absence at {observedMissingAt}");
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
                    // Pull-based reader: the resolver iterates a COW array internally; any snapshot
                    // it touches is immutable post-publication, so no torn state is observable even
                    // while writer is pushing new versions. Discard the result — we only care that
                    // the call returns cleanly under contention.
                    _ = registry.GetActiveSid(WALLET_A);
                }
            });

            Assert.DoesNotThrow(() => Task.WaitAll(new[] { writer, reader }, millisecondsTimeout: 5000));
        }

        [Test]
        public void HasAudioStreamReturnsFalseAfterLastSidUnsubscribed()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            RaiseTrackUnsubscribed(WALLET_A, SID_1);

            Assert.That(registry.HasAudioStream(WALLET_A), Is.False);
            Assert.That(registry.GetActiveSid(WALLET_A), Is.Null);
        }

        // ── Active-sid resolver (frame-activity oracle) ──────────────

        [Test]
        public void ReturnNullActiveSidForUnknownWallet()
        {
            Assert.That(registry.GetActiveSid("0xUNKNOWN"), Is.Null);
        }

        [Test]
        public void ReturnSingleSidAsActiveWithoutConsultingFrameOracle()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);

            Assert.That(registry.GetActiveSid(WALLET_A), Is.EqualTo(SID_1));
            audioStreams.DidNotReceiveWithAnyArgs().GetLastFrameReceivedAt(default);
        }

        [Test]
        public void ReturnNullActiveSidWhenAllCandidatesAreGhosts()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio);
            // Both stay at the default -1 sentinel — none have ever decoded a frame.

            Assert.That(registry.GetActiveSid(WALLET_A), Is.Null);
        }

        [Test]
        public void PickTheOnlyCandidateWithFrameActivity()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio);

            audioStreams.GetLastFrameReceivedAt(new StreamKey(WALLET_A, SID_2)).Returns(100);
            // SID_1 keeps the default -1 sentinel (ghost).

            Assert.That(registry.GetActiveSid(WALLET_A), Is.EqualTo(SID_2));
        }

        [Test]
        public void PickTheNewestCandidateWhenSeveralAreLive()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio);

            audioStreams.GetLastFrameReceivedAt(new StreamKey(WALLET_A, SID_1)).Returns(100);
            audioStreams.GetLastFrameReceivedAt(new StreamKey(WALLET_A, SID_2)).Returns(200);

            Assert.That(registry.GetActiveSid(WALLET_A), Is.EqualTo(SID_2));
        }

        [Test]
        public void TreatTickCounterWrapAroundAsNewerNotOlder()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio);

            // Pre-wrap (older), positive end of the range.
            audioStreams.GetLastFrameReceivedAt(new StreamKey(WALLET_A, SID_1)).Returns(int.MaxValue - 50);
            // Post-wrap (newer in unchecked arithmetic), negative end of the range.
            audioStreams.GetLastFrameReceivedAt(new StreamKey(WALLET_A, SID_2)).Returns(int.MinValue + 50);

            Assert.That(registry.GetActiveSid(WALLET_A), Is.EqualTo(SID_2),
                "unchecked(SID_2_tick - SID_1_tick) == 101 > 0 — resolver must prefer the post-wrap candidate");
        }

        [Test]
        public void IsActiveSidTrueForWinnerFalseForGhostLoser()
        {
            RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio);
            RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio);

            audioStreams.GetLastFrameReceivedAt(new StreamKey(WALLET_A, SID_2)).Returns(500);
            // SID_1 keeps -1 (ghost).

            Assert.That(registry.IsActiveSid(new StreamKey(WALLET_A, SID_2)), Is.True);
            Assert.That(registry.IsActiveSid(new StreamKey(WALLET_A, SID_1)), Is.False);
        }

        [Test]
        public void IsActiveSidFalseWhenWalletHasNoSids()
        {
            Assert.That(registry.IsActiveSid(new StreamKey("0xUNKNOWN", SID_1)), Is.False);
        }

        [Test]
        public void ConcurrentSubUnsubUnderContentionDoesNotLoseUpdates()
        {
            // Three concurrent FFI-thread paths racing on the same wallet. After all complete,
            // sid-B must remain (sub never undone) and reference invariants must not be torn —
            // every snapshot we observe is a COW array, never mutated post-publication.
            const int RUNS = 200;

            for (int run = 0; run < RUNS; run++)
            {
                Task t1 = Task.Run(() => RaiseTrackSubscribed(WALLET_A, SID_1, TrackKind.KindAudio));
                Task t2 = Task.Run(() => RaiseTrackSubscribed(WALLET_A, SID_2, TrackKind.KindAudio));
                Task.WaitAll(t1, t2);

                Task t3 = Task.Run(() => RaiseTrackUnsubscribed(WALLET_A, SID_1));
                t3.Wait();

                Assert.That(ContainsSid(WALLET_A, SID_2), Is.True, "sid-B must survive concurrent contention");

                // Cleanup for next iteration.
                RaiseTrackUnsubscribed(WALLET_A, SID_2);
            }
        }

        // Post-dedup the registry no longer exposes the underlying sid array. For tests that
        // subscribe a single sid, IsActiveSid is an equivalent presence probe (single candidate
        // is automatically the active pick by hot-path contract). Multi-sid tests that depended
        // on per-sid index probing were either deleted (reference-equality on the array) or were
        // already updated to use the resolver semantics directly.
        private bool ContainsSid(string walletId, string sid) =>
            registry.IsActiveSid(new StreamKey(walletId, sid));

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
            var tracksDict = (IDictionary<string, TrackPublication>)tracksField.GetValue(participant)!;
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
