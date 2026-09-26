using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace Services
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioService : MonoBehaviour
    {
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip uiHoverClip;

        public static AudioService Instance { get; private set; }

        private AudioSource _audioSource;

        private void Awake()
        {
            Assert.IsNull(Instance);
            Instance = this;
            _audioSource = GetComponent<AudioSource>();
        }

        public void PlaySFX(SFXType sfxType)
        {
            switch (sfxType)
            {
                case SFXType.UIClick:
                    _audioSource.PlayOneShot(uiClickClip);
                    break;
                case SFXType.UIHover:
                    _audioSource.PlayOneShot(uiHoverClip);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sfxType), sfxType, null);
            }
        }
    }

    public enum SFXType
    {
        UIClick,
        UIHover
    }
}