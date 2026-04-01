using UnityEngine;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Marks a remote entity as having an associated proximity audio source.
    /// Position is synced each frame by <see cref="ProximityAudioPositionSystem"/>.
    /// </summary>
    public struct ProximityAudioSourceComponent
    {
        public AudioSource AudioSource;
        public Transform Transform;

        public ProximityAudioSourceComponent(AudioSource audioSource)
        {
            AudioSource = audioSource;
            Transform = audioSource.transform;
        }
    }
}
