using UnityEngine;

namespace DCL.Audio
{
    public class CinemachineCameraAudioSettings : MonoBehaviour, ICinemachineCameraAudioSettings
    {
        [Header("Audio")]
        [SerializeField] private AudioClipConfig zoomInAudio;
        [SerializeField] private AudioClipConfig zoomOutAudio;

        AudioClipConfig ICinemachineCameraAudioSettings.ZoomInAudio => zoomInAudio;
        AudioClipConfig ICinemachineCameraAudioSettings.ZoomOutAudio => zoomOutAudio;
    }

    public interface ICinemachineCameraAudioSettings
    {
        AudioClipConfig ZoomInAudio { get; }
        AudioClipConfig ZoomOutAudio { get; }
    }
}
