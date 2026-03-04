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

        public ProximityAudioSourceComponent(Transform source)
        {
            AudioSourceTransform = source;
        }
    }
}
