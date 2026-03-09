using LiveKit.Rooms.Streaming.Audio;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Calculates stereo pan for a <see cref="LivekitAudioSource"/> based on
    /// the horizontal angle between the AudioListener and this transform.
    /// Pan is blended by <see cref="AudioSource.spatialBlend"/> so 2D sources
    /// stay centered.
    /// </summary>
    [RequireComponent(typeof(LivekitAudioSource))]
    public class ProximityPanCalculator : MonoBehaviour
    {
        private Transform listenerTransform;
        private LivekitAudioSource livekitAudioSource;
        private AudioSource audioSource;

        private void Awake()
        {
            livekitAudioSource = GetComponent<LivekitAudioSource>();
            audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (listenerTransform == null)
            {
                var listener = FindAnyObjectByType<AudioListener>();
                if (listener == null) return;
                listenerTransform = listener.transform;
            }

            Vector3 direction = transform.position - listenerTransform.position;
            Vector3 local = listenerTransform.InverseTransformDirection(direction);

            float rawPan = Mathf.Atan2(local.x, local.z) / (Mathf.PI * 0.5f);
            float pan = Mathf.Clamp(rawPan, -1f, 1f);

            pan *= audioSource.spatialBlend;

            livekitAudioSource.Pan = pan;
        }
    }
}
