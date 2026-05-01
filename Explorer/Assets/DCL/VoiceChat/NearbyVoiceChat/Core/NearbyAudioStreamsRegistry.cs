using DCL.LiveKit.Public;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using RichTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace DCL.VoiceChat.Nearby.Audio
{
    /// <summary>
    ///     Thread-safe mirror of the island room's remote audio sids, indexed by participant identity.
    ///     Acts as the single source of truth that <see cref="DCL.VoiceChat.Nearby.Systems.NearbyAudioBindingSystem"/>
    ///     polls each tick — no events leak into the ECS pipeline.
    ///     <para>
    ///         Lifecycle is tied to the LiveKit room, not to the user's nearby state:
    ///         <list type="bullet">
    ///             <item><c>TrackSubscribed</c> / <c>TrackUnsubscribed</c> — incremental sid add/remove.</item>
    ///             <item><c>ConnectionUpdated(Connected)</c> — full re-hydration from
    ///                 <see cref="LiveKit.Rooms.Participants.IParticipantsHub.RemoteParticipantIdentities"/>
    ///                 (covers tracks subscribed before our handler attached).</item>
    ///             <item><c>ConnectionUpdated(Disconnected)</c> — clear all sids; binding/cleanup systems
    ///                 reap the orphaned audio entities on the next tick.</item>
    ///         </list>
    ///     </para>
    ///     Suppression / mute / block policies do <b>not</b> touch this registry — they are applied as pull-based
    ///     filters in the systems layer so resume / unblock instantly re-bind from the unchanged snapshot.
    ///     <para>
    ///         <b>Immutability contract.</b> Per-identity <c>string[]</c> arrays are copy-on-write
    ///         (every mutation publishes a new reference); the dictionary itself is swapped atomically on rehydrate / disconnect.
    ///         Do <b>not</b> reintroduce in-place mutation (array resize, pooled buffers, dict <c>Clear()</c>+rebuild) —
    ///         the bridge's <c>ReferenceEquals</c> freshness check and the cleanup system's "stream gone" detection both rely on these invariants.
    ///     </para>
    /// </summary>
    public sealed class NearbyAudioStreamsRegistry : INearbyAudioStreamRegistry
    {
        private readonly IRoom room;

        // Immutability contract — see class XML. Swappable via Interlocked.Exchange / Volatile.Read.
        // concurrencyLevel: 1 — FFI dispatch is serial, only one writer ever; saves the per-instance lock array (default = Environment.ProcessorCount).
        private ConcurrentDictionary<string, string[]> streamsByIdentity = NewSnapshot();
        private readonly ConcurrentDictionary<string, byte> activeSpeakers = new ();

        public NearbyAudioStreamsRegistry(IRoom room)
        {
            this.room = room;

            room.ConnectionUpdated += OnConnectionUpdated;

            room.TrackSubscribed += OnTrackSubscribed;
            room.TrackUnsubscribed += OnTrackUnsubscribed;
            room.TrackUnpublished += OnTrackUnpublished;

            room.Participants.UpdatesFromParticipant += OnParticipantUpdate;
            room.ActiveSpeakers.Updated += PullActiveSpeakers;

            if (room.Info.ConnectionState == LKConnectionState.ConnConnected)
            {
                RehydrateFromRoom();
                PullActiveSpeakers();
            }
        }

        public void Dispose()
        {
            room.ConnectionUpdated -= OnConnectionUpdated;

            room.TrackSubscribed -= OnTrackSubscribed;
            room.TrackUnsubscribed -= OnTrackUnsubscribed;
            room.TrackUnpublished -= OnTrackUnpublished;

            room.Participants.UpdatesFromParticipant -= OnParticipantUpdate;
            room.ActiveSpeakers.Updated -= PullActiveSpeakers;

            Interlocked.Exchange(ref streamsByIdentity, NewSnapshot());
            activeSpeakers.Clear();
        }

        private static ConcurrentDictionary<string, string[]> NewSnapshot(int capacity = 0) =>
            new (concurrencyLevel: 1, capacity: capacity);

        // Relies on serial FFI dispatch — concurrent ActiveSpeakers.Updated / OnConnectionUpdated invocations
        // during clear+rebuild are impossible by LiveKit's per-room dispatch contract.
        // The transient "everything empty" window between Clear() and the final TryAdd is acceptable:
        // readers (NearbyAudioBindingSystem and friends) are pull-based per-tick, so a missed-by-one-frame
        // active-speaker flag is corrected on the next Updated event. Do not introduce snapshot-swap here
        // unless a strict point-in-time read becomes a hard requirement.
        private void PullActiveSpeakers()
        {
            activeSpeakers.Clear();
            foreach (string id in room.ActiveSpeakers)
                activeSpeakers.TryAdd(id, 0);
        }

        public bool IsActiveSpeaker(string walletId) =>
            activeSpeakers.ContainsKey(walletId);

        public bool HasAudioStream(string walletId) =>
            Volatile.Read(ref streamsByIdentity).ContainsKey(walletId);

        // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
        public string[]? GetAudioSidsArray(string walletId) =>
            Volatile.Read(ref streamsByIdentity).TryGetValue(walletId, out string[]? arr) ? arr : null;

        public Weak<AudioStream> GetActiveStream(StreamKey key) =>
            room.AudioStreams.ActiveStream(key);

        public bool IsStreamGone(StreamKey key)
        {
            if (!Volatile.Read(ref streamsByIdentity).TryGetValue(key.identity, out string[]? sids))
                return true;

            return Array.IndexOf(sids, key.sid) < 0;
        }

        private void OnConnectionUpdated(IRoom _, ConnectionUpdate update, LKDisconnectReason? __)
        {
            switch (update)
            {
                case ConnectionUpdate.Disconnected:
                    Interlocked.Exchange(ref streamsByIdentity, NewSnapshot());
                    activeSpeakers.Clear();
                    return;
                case ConnectionUpdate.Connected:
                case ConnectionUpdate.Reconnected:
                    RehydrateFromRoom();
                    PullActiveSpeakers();
                    return;
                // Reconnecting: no-op intentionally — per-track events still flow during reconnect,
                // and the following Reconnected triggers a full re-sync that hard-resyncs any drift.
            }
        }

        private void RehydrateFromRoom()
        {
            IReadOnlyDictionary<string, LKParticipant> participants = room.Participants.RemoteParticipantIdentities();
            ConcurrentDictionary<string, string[]> next = NewSnapshot(participants.Count);

            foreach (KeyValuePair<string, LKParticipant> participantEntry in participants)
            foreach (KeyValuePair<string, TrackPublication> trackEntry in participantEntry.Value.Tracks)
                if (IsNearbyAudio(trackEntry.Value))
                    AddAudioSidTo(next, participantEntry.Key, trackEntry.Key);

            Interlocked.Exchange(ref streamsByIdentity, next);
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, LKParticipant participant)
        {
            if (IsNearbyAudio(publication))
                AddAudioSid(participant.Identity, publication.Sid);
        }

        private static bool IsNearbyAudio(TrackPublication publication) =>
            publication is { Kind: TrackKind.KindAudio, Source: TrackSource.SourceMicrophone };

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, LKParticipant participant) =>
            RemoveAudioSid(participant.Identity, publication.Sid);

        private void OnTrackUnpublished(TrackPublication publication, LKParticipant participant)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            // Room.cs:300-301 may invoke with a null publication when participant.UnPublish
            // raced another teardown path and returned null via the out-param.
            if (publication is null) return;

            // No kind/source filter on remove: foreign sids never enter streamsByIdentity (subscribe-side filter), so RemoveAudioSid is a safe no-op for them.
            RemoveAudioSid(participant.Identity, publication.Sid);
        }

        private void OnParticipantUpdate(LKParticipant participant, UpdateFromParticipant update)
        {
            if (update == UpdateFromParticipant.Disconnected)
                Volatile.Read(ref streamsByIdentity).TryRemove(participant.Identity, out _);
        }

        private void AddAudioSid(string identity, string sid) =>
            AddAudioSidTo(Volatile.Read(ref streamsByIdentity), identity, sid);

        private static void AddAudioSidTo(ConcurrentDictionary<string, string[]> dict, string identity, string sid)
        {
            dict.AddOrUpdate(
                identity,
                static (_, addedSid) => new[] { addedSid },
                static (_, prev, addedSid) => Array.IndexOf(prev, addedSid) >= 0 ? prev : ConcatNew(prev, addedSid),
                sid);
        }

        // Publishes a NEW filtered array on every successful update; never mutates the previous one.
        // Single-writer assumption (serial FFI dispatch) — see class XML; no CAS retry needed.
        private void RemoveAudioSid(string identity, string sid)
        {
            ConcurrentDictionary<string, string[]> snap = Volatile.Read(ref streamsByIdentity);

            if (!snap.TryGetValue(identity, out string[]? prev)) return;

            int idx = Array.IndexOf(prev, sid);
            if (idx < 0) return;

            if (prev.Length == 1)
            {
                snap.TryRemove(identity, out _);
                return;
            }

            var next = new string[prev.Length - 1];
            if (idx > 0) Array.Copy(prev, 0, next, 0, idx);
            if (idx < prev.Length - 1) Array.Copy(prev, idx + 1, next, idx, prev.Length - idx - 1);

            snap[identity] = next;
        }

        private static string[] ConcatNew(string[] prev, string sid)
        {
            var next = new string[prev.Length + 1];
            Array.Copy(prev, 0, next, 0, prev.Length);
            next[prev.Length] = sid;
            return next;
        }
    }
}
