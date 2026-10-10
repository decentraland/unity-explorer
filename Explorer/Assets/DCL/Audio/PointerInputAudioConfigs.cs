using UnityEngine;

namespace DCL.Audio
{
    public interface IPointerInputAudioConfigs
    {
        AudioClipConfig PointerAudio { get; }
        AudioClipConfig PrimaryAudio { get; }
        AudioClipConfig SecondaryAudio { get; }
    }

    [CreateAssetMenu(fileName = "PointerInputAudioConfigs", menuName = "DCL/Audio/Pointer Input Audio Configs")]
    public class PointerInputAudioConfigs : ScriptableObject, IPointerInputAudioConfigs
    {
        [SerializeField] private AudioClipConfig primaryAudio;
        [SerializeField] private AudioClipConfig pointerAudio;
        [SerializeField] private AudioClipConfig secondaryAudio;
        public AudioClipConfig PointerAudio => pointerAudio;
        public AudioClipConfig PrimaryAudio => primaryAudio;
        public AudioClipConfig SecondaryAudio => secondaryAudio;
    }
}
