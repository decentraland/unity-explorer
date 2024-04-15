using DCL.Diagnostics;
using DG.Tweening;
using System;
using UnityEngine;

namespace DCL.Audio
{
    public class UIAudioPlaybackController : MonoBehaviour, IDisposable
    {
        //We need different Audio Sources to handle the different volume configurations each category can have.
        //So we could have for example silenced UI audio, max out music and 50% on Chat sounds.

        [SerializeField]
        private AudioSource UiAudioSource;
        [SerializeField]
        private AudioSource MusicAudioSource;
        [SerializeField]
        private AudioSource ChatAudioSource;
        [SerializeField]
        private float fadeDuration = 1.5f;
        [SerializeField]
        private AudioSettings audioSettings;
        [SerializeField]
        private AudioSource TestWorldAudioSource;


        private Tweener loopingAudioTweener;

        public void Dispose()
        {
            UIAudioEventsBus.Instance.PlayUIAudioEvent -= OnPlayUIAudioEvent;
            UIAudioEventsBus.Instance.PlayLoopingUIAudioEvent -= OnPlayLoopingUIAudioEvent;
            UiAudioSource.Stop();
            MusicAudioSource.Stop();
            ChatAudioSource.Stop();
            loopingAudioTweener.Kill();
        }

        public void Initialize()
        {
            UIAudioEventsBus.Instance.PlayUIAudioEvent += OnPlayUIAudioEvent;
            UIAudioEventsBus.Instance.PlayLoopingUIAudioEvent += OnPlayLoopingUIAudioEvent;
            UIAudioEventsBus.Instance.PlayDefaultAudioEvent += OnPlayDefaultAudio;
        }

        private void OnPlayDefaultAudio(float volume)
        {
            TestWorldAudioSource.volume = volume;
            if (!TestWorldAudioSource.isPlaying)
            {
                TestWorldAudioSource.Play();
            }
        }

        private void OnPlayLoopingUIAudioEvent(AudioClipConfig audioClipConfig, bool startLoop)
        {
            if (!CheckAudioCategory(audioClipConfig)) return;

            AudioCategorySettings settings = audioSettings.GetSettingsForCategory(audioClipConfig.Category);
            if (!settings.AudioEnabled) return;

            AudioSource audioSource = GetAudioSourceForCategory(audioClipConfig.Category);

            loopingAudioTweener.Kill();

            if (audioSource.isPlaying) { loopingAudioTweener = audioSource.DOFade(0, fadeDuration).OnComplete(() => ContinuePlayLoopingUIAudio(audioSource, startLoop, audioClipConfig)); }
            else { ContinuePlayLoopingUIAudio(audioSource, startLoop, audioClipConfig); }
        }

        private void ContinuePlayLoopingUIAudio(AudioSource audioSource, bool startLoop, AudioClipConfig audioClipConfig)
        {
            if (startLoop)
            {
                int clipIndex = AudioPlaybackUtilities.GetClipIndex(audioClipConfig);
                audioSource.clip = audioClipConfig.AudioClips[clipIndex];
                audioSource.Play();
                audioSource.DOFade(audioClipConfig.RelativeVolume, fadeDuration);
            }
            else { audioSource.Stop(); }
        }

        private void OnPlayUIAudioEvent(AudioClipConfig audioClipConfig)
        {
            if ( CheckAudioClips(audioClipConfig) || !CheckAudioCategory(audioClipConfig)) return;

            AudioCategorySettings settings = audioSettings.GetSettingsForCategory(audioClipConfig.Category);
            if (!settings.AudioEnabled) return;

            PlaySingleAudio(audioClipConfig);
        }

        private bool CheckAudioClips(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig.AudioClips.Length == 0)
            {
                ReportHub.LogError(new ReportData(ReportCategory.AUDIO), $"Cannot Play Audio {audioClipConfig.name} as it has no Audio Clips Assigned");
                return false;
            }
            else
            {
                return true;
            }
        }



        private bool CheckAudioCategory(AudioClipConfig audioClipConfig)
        {
            //We can only play UI sounds through this bus. Other sounds are discarded
            if (audioClipConfig.Category is not (AudioCategory.Chat or AudioCategory.Music or AudioCategory.UI))
            {
                ReportHub.LogError(new ReportData(ReportCategory.AUDIO), $"Cannot Play Audio {audioClipConfig.name} as it is from category {audioClipConfig.Category} and this bus only supports Chat, Music or UI");
                return false;
            }

            return true;
        }

        private void PlaySingleAudio(AudioClipConfig audioClipConfig)
        {
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(audioClipConfig);
            AudioSource audioSource = GetAudioSourceForCategory(audioClipConfig.Category);
            audioSource.pitch = AudioPlaybackUtilities.GetPitchWithVariation(audioClipConfig);
            audioSource.PlayOneShot(audioClipConfig.AudioClips[clipIndex], audioClipConfig.RelativeVolume);
        }

        private AudioSource GetAudioSourceForCategory(AudioCategory audioCategory)
        {
            switch (audioCategory)
            {
                case AudioCategory.UI: return UiAudioSource;
                case AudioCategory.Chat: return ChatAudioSource;
                case AudioCategory.Music: return MusicAudioSource;
                case AudioCategory.World:
                case AudioCategory.Avatar:
                case AudioCategory.None:
                default: throw new ArgumentOutOfRangeException(nameof(audioCategory), audioCategory, null);
            }
        }
    }
}
