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
    /// </summary>
    public sealed class NearbyAudioStreamRegistry : INearbyAudioStreamRegistry
    {
        private readonly IRoom room;

        // Per-wallet sid set storage.
        // ── Reference-equality contract ──────────────────────────────────────
        // Every mutation (OnTrackSubscribed / OnTrackUnsubscribed / ConnectionUpdated) MUST
        // produce a brand-new string[] reference. Never mutate or reuse arrays in place.
        // Reference identity is the version signal observed by
        // NearbyLivekitBridgeSystem.UpdateStreaming: ReferenceEquals(observed, current) ↔ content
        // unchanged. Pool reuse / Array.Resize / in-place edits silently break that invariant.
        private readonly ConcurrentDictionary<string, string[]> streamsByIdentity = new ();
        private readonly ConcurrentDictionary<string, byte> activeSpeakers = new ();

        public NearbyAudioStreamRegistry(IRoom room)
        {
            this.room = room;

            room.ConnectionUpdated += OnConnectionUpdated;

            room.TrackSubscribed += OnTrackSubscribed;
            room.TrackUnsubscribed += OnTrackUnsubscribed;

            room.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
        }

        public void Dispose()
        {
            room.ConnectionUpdated -= OnConnectionUpdated;

            room.TrackSubscribed -= OnTrackSubscribed;
            room.TrackUnsubscribed -= OnTrackUnsubscribed;

            room.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;

            streamsByIdentity.Clear();
            activeSpeakers.Clear();
        }

        private void OnActiveSpeakersUpdated()
        {
            activeSpeakers.Clear();
            foreach (string id in room.ActiveSpeakers)
                activeSpeakers.TryAdd(id, 0);
        }

        public bool IsActiveSpeaker(string walletId) =>
            activeSpeakers.ContainsKey(walletId);

        public bool HasAudioStream(string walletId) =>
            streamsByIdentity.ContainsKey(walletId);

        public ReadOnlySpan<string> GetAudioSids(string walletId) =>
            streamsByIdentity.TryGetValue(walletId, out string[]? arr) ? arr : default;

        // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault - ConcurrentDictionary.TryGetValue is more optimized, no virtual call
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
            if (update == ConnectionUpdate.Disconnected)
            {
                streamsByIdentity.Clear();
                activeSpeakers.Clear();
                return;
            }

            if (update != ConnectionUpdate.Connected) return;

            streamsByIdentity.Clear();

            foreach (KeyValuePair<string, LKParticipant> participantEntry in room.Participants.RemoteParticipantIdentities())
            foreach (KeyValuePair<string, TrackPublication> trackEntry in participantEntry.Value.Tracks)
                if (trackEntry.Value.Kind == TrackKind.KindAudio)
                    AddAudioSid(participantEntry.Key, trackEntry.Key);
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, LKParticipant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
                AddAudioSid(participant.Identity, publication.Sid);
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, LKParticipant participant)
        {
            string identity = participant.Identity;
            string sid = publication.Sid;

            // CAS retry against concurrent FFI events on the same wallet. Each attempt produces
            // a freshly-allocated array — never mutate `prev` in place.
            while (true)
            {
                if (!streamsByIdentity.TryGetValue(identity, out string[]? prev))
                    return;

                int idx = Array.IndexOf(prev, sid);
                if (idx < 0) return; // sid already gone (concurrent unsubscribe or never present)

                if (prev.Length == 1)
                {
                    // Drop the wallet entry only when the prev reference still matches; otherwise loop.
                    var pair = new KeyValuePair<string, string[]>(identity, prev);
                    if (((ICollection<KeyValuePair<string, string[]>>)streamsByIdentity).Remove(pair))
                        return;

                    continue;
                }

                string[] next = new string[prev.Length - 1];
                for (int i = 0, j = 0; i < prev.Length; i++)
                {
                    if (i == idx) continue;
                    next[j++] = prev[i];
                }

                if (streamsByIdentity.TryUpdate(identity, next, prev))
                    return;
            }
        }

        private void AddAudioSid(string identity, string sid)
        {
            // AddOrUpdate's update factory may run multiple times under contention — every
            // invocation must produce a NEW array (never mutate `prev`) so reference-equality
            // remains the version signal.
            streamsByIdentity.AddOrUpdate(
                identity,
                static (_, addedSid) => new[] { addedSid },
                static (_, prev, addedSid) =>
                {
                    if (Array.IndexOf(prev, addedSid) >= 0)
                        return prev;

                    string[] next = new string[prev.Length + 1];
                    Array.Copy(prev, next, prev.Length);
                    next[prev.Length] = addedSid;
                    return next;
                },
                sid);
        }
    }
}
