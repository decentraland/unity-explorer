using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Marks a remote entity as having an associated proximity audio source.
    /// Position is synced each frame by <see cref="ProximityAudioPositionSystem"/>.
    /// </summary>
    public struct ProximityAudioSourceComponent
    {
        public Transform AudioSourceTransform;
        public AudioSource AudioSource;

        public ProximityAudioSourceComponent(AudioSource audioSource)
        {
            AudioSource = audioSource;
            AudioSourceTransform = audioSource.transform;
        }
    }
}
