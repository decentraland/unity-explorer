using LiveKit.Rooms.Streaming.Audio;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Calculates 3D spatial angles (azimuth and elevation) between the AudioListener
    /// and this transform, then passes them to <see cref="LivekitAudioSource.SetSpatialAngles"/>.
    /// The spatialization algorithm is selected on <see cref="LivekitAudioSource"/> via Inspector enum.
    /// </summary>
    [RequireComponent(typeof(LivekitAudioSource))]
    public class ProximityPanCalculator : MonoBehaviour
    {
        private Transform listenerTransform;
        private LivekitAudioSource livekitAudioSource;

        private void Awake()
        {
            livekitAudioSource = GetComponent<LivekitAudioSource>();
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

            float azimuth = Mathf.Atan2(local.x, local.z);
            float horizontalDist = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            float elevation = Mathf.Atan2(local.y, horizontalDist);

            livekitAudioSource.SetSpatialAngles(azimuth, elevation);
        }
    }
}
