using LiveKit.Rooms.Streaming.Audio;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Marks a remote entity as having an associated nearby audio source.
    /// Position is synced each frame by <see cref="NearbyAudioPositionSystem"/>.
    /// </summary>
    public struct NearbyAudioSourceComponent
    {
        public readonly string ParticipantIdentity;

        public LivekitAudioSource LivekitAudioSource;
        public Transform Transform;

        public NearbyAudioSourceComponent(string participantIdentity, LivekitAudioSource livekitAudioSource)
        {
            ParticipantIdentity = participantIdentity;
            LivekitAudioSource = livekitAudioSource;

            Transform = livekitAudioSource.transform;
        }
    }
}
