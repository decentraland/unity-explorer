using UnityEngine;

namespace DCL.Audio
{
    public interface IInteractionsAudioConfigs
    {
        AudioClipConfig PointerAudio { get; }
        AudioClipConfig PrimaryAudio { get; }
        AudioClipConfig SecondaryAudio { get; }
    }

    [CreateAssetMenu(fileName = "InteractionsAudioConfigs", menuName = "SO/Audio/InteractionsAudioConfigs")]
    public class InteractionsAudioConfigs : ScriptableObject, IInteractionsAudioConfigs
    {
        [SerializeField] private AudioClipConfig primaryAudio;
        [SerializeField] private AudioClipConfig pointerAudio ;
        [SerializeField] private AudioClipConfig secondaryAudio;
        public AudioClipConfig PointerAudio => pointerAudio;
        public AudioClipConfig PrimaryAudio => primaryAudio;
        public AudioClipConfig SecondaryAudio => secondaryAudio;
    }
}
