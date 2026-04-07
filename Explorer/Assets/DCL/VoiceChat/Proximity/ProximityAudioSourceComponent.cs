using LiveKit.Rooms.Streaming.Audio;
using UnityEngine;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Marks a remote entity as having an associated proximity audio source.
    /// Position is synced each frame by <see cref="ProximityAudioPositionSystem"/>.
    /// </summary>
    public struct ProximityAudioSourceComponent
    {
        public LivekitAudioSource LivekitAudioSource;
        public Transform Transform;
        public string ParticipantIdentity;

        public ProximityAudioSourceComponent(LivekitAudioSource livekitAudioSource, string participantIdentity)
        {
            LivekitAudioSource = livekitAudioSource;
            Transform = livekitAudioSource.transform;
            ParticipantIdentity = participantIdentity;
        }
    }
}
