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
    ///         <b>Reference-equality contract.</b> <see cref="streamsByIdentity"/> stores per-wallet sid sets as
    ///         immutable <c>string[]</c> arrays under copy-on-write semantics. Every mutation
    ///         (<see cref="AddAudioSid"/> / <see cref="RemoveAudioSid"/> / <see cref="RehydrateFromRoom"/>)
    ///         publishes a <b>new</b> array reference — never reuses or mutates an existing one. This invariant
    ///         is load-bearing for <see cref="DCL.VoiceChat.Nearby.Systems.NearbyLivekitBridgeSystem"/>'s
    ///         <c>UpdateStreamingQuery</c>: <c>ReferenceEquals(observed, current)</c> is the freshness check
    ///         that replaces a version counter. Do <b>not</b> introduce <c>Array.Resize</c>, pooled buffers,
    ///         or in-place mutation under any future "optimization" — it would silently break the bridge.
    ///     </para>
    /// </summary>
    public sealed class NearbyAudioStreamsRegistry : INearbyAudioStreamRegistry
    {
        private readonly IRoom room;

        // Reference-equality contract — see class XML. Each value is a freshly-allocated, immutable string[]
        // published by AddAudioSid / RemoveAudioSid / RehydrateFromRoom. Never mutate.
        private readonly ConcurrentDictionary<string, string[]> streamsByIdentity = new ();
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
        }

        public void Dispose()
        {
            room.ConnectionUpdated -= OnConnectionUpdated;

            room.TrackSubscribed -= OnTrackSubscribed;
            room.TrackUnsubscribed -= OnTrackUnsubscribed;
            room.TrackUnpublished -= OnTrackUnpublished;

            room.Participants.UpdatesFromParticipant -= OnParticipantUpdate;
            room.ActiveSpeakers.Updated -= PullActiveSpeakers;

            streamsByIdentity.Clear();
            activeSpeakers.Clear();
        }

        private void PullActiveSpeakers()
        {
            activeSpeakers.Clear();
            foreach (string id in room.ActiveSpeakers)
                activeSpeakers.TryAdd(id, 0);
        }

        public bool IsActiveSpeaker(string walletId) =>
            activeSpeakers.ContainsKey(walletId);

        public bool HasAudioStream(string walletId) =>
            streamsByIdentity.ContainsKey(walletId);

        // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
        public string[]? GetAudioSidsArray(string walletId) =>
            streamsByIdentity.TryGetValue(walletId, out string[]? arr) ? arr : null;

        public Weak<AudioStream> GetActiveStream(StreamKey key) =>
            room.AudioStreams.ActiveStream(key);

        public bool IsStreamGone(StreamKey key)
        {
            if (!streamsByIdentity.TryGetValue(key.identity, out string[]? sids))
                return true;

            return Array.IndexOf(sids, key.sid) < 0;
        }

        private void OnConnectionUpdated(IRoom _, ConnectionUpdate update, LKDisconnectReason? __)
        {
            switch (update)
            {
                case ConnectionUpdate.Disconnected:
                    streamsByIdentity.Clear();
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

        // Relies on serial FFI dispatch — concurrent OnTrackSubscribed/OnTrackUnsubscribed during
        // clear+rebuild is impossible by LiveKit's per-room dispatch contract.
        private void RehydrateFromRoom()
        {
            streamsByIdentity.Clear();

            foreach (KeyValuePair<string, LKParticipant> participantEntry in room.Participants.RemoteParticipantIdentities())
            foreach (KeyValuePair<string, TrackPublication> trackEntry in participantEntry.Value.Tracks)
                if (IsNearbyAudio(trackEntry.Value))
                    AddAudioSid(participantEntry.Key, trackEntry.Key);
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
                streamsByIdentity.TryRemove(participant.Identity, out _);
        }

        // Reference-equality contract — produces a NEW array on every call, never mutates an existing one.
        private void AddAudioSid(string identity, string sid)
        {
            streamsByIdentity.AddOrUpdate(
                identity,
                static (_, addedSid) => new[] { addedSid },
                static (_, prev, addedSid) => Array.IndexOf(prev, addedSid) >= 0 ? prev : ConcatNew(prev, addedSid),
                sid);
        }

        // Reference-equality contract — CAS-retry loop publishes a NEW filtered array on every successful
        // update; never mutates the previous one.
        private void RemoveAudioSid(string identity, string sid)
        {
            // ConcurrentDictionary<TKey,TValue>.TryRemove(KeyValuePair) is .NET Core 2.0+ only;
            // Fall back to ICollection<KVP>.Remove(item), which performs the same atomic key+value compare under the hood .
            var coll = (ICollection<KeyValuePair<string, string[]>>)streamsByIdentity;

            while (streamsByIdentity.TryGetValue(identity, out string[]? prev))
            {
                int idx = Array.IndexOf(prev, sid);
                if (idx < 0) return;

                if (prev.Length == 1)
                {
                    if (coll.Remove(new KeyValuePair<string, string[]>(identity, prev)))
                        return;
                    // Lost CAS — another FFI event mutated the entry; retry from the new snapshot.
                    continue;
                }

                var next = new string[prev.Length - 1];
                if (idx > 0) Array.Copy(prev, 0, next, 0, idx);
                if (idx < prev.Length - 1) Array.Copy(prev, idx + 1, next, idx, prev.Length - idx - 1);

                if (streamsByIdentity.TryUpdate(identity, next, prev))
                    return;
                // Lost CAS — retry from the new snapshot.
            }
        }

        // Reference-equality contract — always allocates a fresh array.
        private static string[] ConcatNew(string[] prev, string sid)
        {
            var next = new string[prev.Length + 1];
            Array.Copy(prev, 0, next, 0, prev.Length);
            next[prev.Length] = sid;
            return next;
        }
    }
}
