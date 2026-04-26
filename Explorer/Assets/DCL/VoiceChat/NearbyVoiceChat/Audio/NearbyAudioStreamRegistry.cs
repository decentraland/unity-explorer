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
    public sealed class NearbyAudioStreamRegistry : INearbyAudioStreamRegistry
    {
        private readonly IRoom room;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> streamsByIdentity = new ();

        public NearbyAudioStreamRegistry(IRoom room)
        {
            this.room = room;

            room.ConnectionUpdated += OnConnectionUpdated;

            room.TrackSubscribed += OnTrackSubscribed;
            room.TrackUnsubscribed += OnTrackUnsubscribed;
        }

        public void Dispose()
        {
            room.ConnectionUpdated -= OnConnectionUpdated;

            room.TrackSubscribed -= OnTrackSubscribed;
            room.TrackUnsubscribed -= OnTrackUnsubscribed;

            streamsByIdentity.Clear();
        }

        // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault - ConcurrentDictionary.TryGetValue is more optimized, no virtal call
        public ConcurrentDictionary<string, byte>? GetAudioSids(string walletId) =>
            streamsByIdentity.TryGetValue(walletId, out ConcurrentDictionary<string, byte>? sids) ? sids : null;

        public Weak<AudioStream> GetActiveStream(StreamKey key) =>
            room.AudioStreams.ActiveStream(key);

        private void OnConnectionUpdated(IRoom _, ConnectionUpdate update, LKDisconnectReason? __)
        {
            if (update == ConnectionUpdate.Disconnected)
            {
                streamsByIdentity.Clear();
                return;
            }

            if (update != ConnectionUpdate.Connected) return;

            streamsByIdentity.Clear();

            foreach (KeyValuePair<string, LKParticipant> entry in room.Participants.RemoteParticipantIdentities())
            foreach (KeyValuePair<string, TrackPublication> trackEntry in entry.Value.Tracks)
                if (trackEntry.Value.Kind == TrackKind.KindAudio)
                    AddAudioSid(entry.Key, trackEntry.Key);
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
