using DCL.LiveKit.Public;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using RichTypes;
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
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> streamsByIdentity = new ();
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

        // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault - ConcurrentDictionary.TryGetValue is more optimized, no virtal call
        public ConcurrentDictionary<string, byte>? GetAudioSids(string walletId) =>
            streamsByIdentity.TryGetValue(walletId, out ConcurrentDictionary<string, byte>? sids) ? sids : null;

        public Weak<AudioStream> GetActiveStream(StreamKey key) =>
            room.AudioStreams.ActiveStream(key);

        public bool IsStreamGone(StreamKey key)
        {
            ConcurrentDictionary<string, byte>? sids = GetAudioSids(key.identity);
            return sids == null || !sids.ContainsKey(key.sid);
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
            if (!streamsByIdentity.TryGetValue(participant.Identity, out ConcurrentDictionary<string, byte>? sids))
                return;

            sids.TryRemove(publication.Sid, out _);

            if (sids.IsEmpty)
                streamsByIdentity.TryRemove(participant.Identity, out _);
        }

        private void AddAudioSid(string identity, string sid)
        {
            ConcurrentDictionary<string, byte> sids = streamsByIdentity.GetOrAdd(identity, static _ => new ConcurrentDictionary<string, byte>());
            sids.TryAdd(sid, 0);
        }
    }
}
