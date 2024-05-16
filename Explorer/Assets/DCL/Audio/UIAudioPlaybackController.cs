using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DG.Tweening;
using DG.Tweening.Core;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

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
        private AudioSource audioSourcePrefab;

        private readonly Dictionary<AudioClipConfig, ContinuousPlaybackAudioData> audioDataPerAudioClipConfig = new ();

        private GameObjectPool<AudioSource> audioSourcePool;
        private CancellationTokenSource mainCancellationTokenSource;

        public void Dispose()
        {
            UIAudioEventsBus.Instance.PlayUIAudioEvent -= OnPlayUIAudioEvent;
            UIAudioEventsBus.Instance.PlayContinuousUIAudioEvent -= OnPlayContinuousUIAudioEvent;
            UIAudioEventsBus.Instance.StopContinuousUIAudioEvent -= OnStopContinuousUIAudioEvent;
            mainCancellationTokenSource.SafeCancelAndDispose();

            foreach (KeyValuePair<AudioClipConfig, ContinuousPlaybackAudioData> audioData in audioDataPerAudioClipConfig)
            {
                //We do this in case a fadeout is being carried out when we Dispose
                audioData.Value.FadeTweener.Kill();
            }

            UiAudioSource.Stop();
            MusicAudioSource.Stop();
            ChatAudioSource.Stop();
            audioDataPerAudioClipConfig.Clear();
            audioSourcePool?.Dispose();
        }

        public void Initialize()
        {
            UIAudioEventsBus.Instance.PlayUIAudioEvent += OnPlayUIAudioEvent;
            UIAudioEventsBus.Instance.PlayContinuousUIAudioEvent += OnPlayContinuousUIAudioEvent;
            UIAudioEventsBus.Instance.StopContinuousUIAudioEvent += OnStopContinuousUIAudioEvent;
            audioSourcePool = new GameObjectPool<AudioSource>(transform, OnCreateAudioSource);
            mainCancellationTokenSource = new CancellationTokenSource();
        }

        private CancellationTokenSource CreateLinkedCancellationTokenSource() =>
            CancellationTokenSource.CreateLinkedTokenSource(mainCancellationTokenSource.Token);

        private AudioSource OnCreateAudioSource()
        {
            AudioSource audioSource = Instantiate(audioSourcePrefab, transform);
            audioSource.spatialBlend = 1;
            return audioSource;
        }

        private void OnStopContinuousUIAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioDataPerAudioClipConfig.TryGetValue(audioClipConfig, out ContinuousPlaybackAudioData audioData))
            {
                audioData.FadeTweener.Kill();
                audioData.CancellationTokenSource.SafeCancelAndDispose();

                audioData.FadeTweener = audioData.AudioSource.DOFade(0, fadeDuration)
                                                 .SetAutoKill()
                                                 .OnComplete(() =>
                                                  {
                                                      if (!mainCancellationTokenSource.IsCancellationRequested)
                                                          ReleaseOnFadeOut(audioData, audioClipConfig);
                                                  });
            }
        }

        private void ReleaseOnFadeOut(ContinuousPlaybackAudioData audioData, AudioClipConfig audioClipConfig)
        {
            audioData.AudioSource.Stop();
            audioSourcePool.Release(audioData.AudioSource);
            audioDataPerAudioClipConfig.Remove(audioClipConfig);
        }

        private void OnPlayContinuousUIAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (!CheckAudioCategory(audioClipConfig) || !CheckAudioClips(audioClipConfig)) return;

            AudioCategorySettings settings = audioSettings.GetSettingsForCategory(audioClipConfig.Category);

            if (settings == null || !settings.AudioEnabled) return;

            //If the audioClipConfig is already in the dictionary, means its already being played, so we just skip this event
            if (audioDataPerAudioClipConfig.ContainsKey(audioClipConfig)) return;

            CancellationTokenSource cts = CreateLinkedCancellationTokenSource();

            AudioSource audioSource = GetAudioSourceFromPoolForCategory(audioClipConfig.Category);
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(audioClipConfig);
            audioSource.clip = audioClipConfig.AudioClips[clipIndex];
            audioSource.Play();

            Tweener fadeTween = audioSource.DOFade(audioClipConfig.RelativeVolume, fadeDuration).OnComplete(() => StartPlayingContinuousUIAudio(audioSource, audioClipConfig, cts.Token));

            var audioData = new ContinuousPlaybackAudioData(audioSource, fadeTween, cts);
            audioDataPerAudioClipConfig.Add(audioClipConfig, audioData);
        }

        private void StartPlayingContinuousUIAudio(AudioSource audioSource, AudioClipConfig audioClipConfig, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            if (audioClipConfig.AudioClips.Length == 1)
            {
                audioSource.loop = true;
                return;
            }

            AudioPlaybackUtilities.SchedulePlaySoundAsync(ct, audioClipConfig, audioSource.clip.length, audioSource).Forget();
        }

        private void OnPlayUIAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (!CheckAudioClips(audioClipConfig) || !CheckAudioCategory(audioClipConfig)) return;

            AudioCategorySettings settings = audioSettings.GetSettingsForCategory(audioClipConfig.Category);

            if (settings == null || !settings.AudioEnabled) return;

            PlaySingleAudio(audioClipConfig);
        }

        private bool CheckAudioClips(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig.AudioClips.Length == 0)
            {
                ReportHub.Log(new ReportData(ReportCategory.AUDIO), $"Cannot Play Audio {audioClipConfig.name} as it has no Audio Clips Assigned");
                return false;
            }

            if (audioClipConfig.RelativeVolume == 0)
            {
                ReportHub.Log(new ReportData(ReportCategory.AUDIO), $"Cannot Play Audio {audioClipConfig.name} as it has a Relative Volume of 0");
                return false;
            }

            return true;
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
            if (audioClipConfig.AudioClips[clipIndex] == null) return;

            float pitch = AudioPlaybackUtilities.GetPitchWithVariation(audioClipConfig);
            AudioSource audioSource;

            if (pitch == 0)
            {
                audioSource = GetDefaultAudioSourceForCategory(audioClipConfig.Category);
                audioSource.PlayOneShot(audioClipConfig.AudioClips[clipIndex], audioClipConfig.RelativeVolume);
            }
            else
            {
                audioSource = GetAudioSourceFromPoolForCategory(audioClipConfig.Category);
                audioSource.clip = audioClipConfig.AudioClips[clipIndex];
                audioSource.pitch = pitch;
                audioSource.loop = false;
                audioSource.Play();
                ScheduleAudioSourceReleaseAsync(audioSource).Forget();
            }
        }

        private async UniTask ScheduleAudioSourceReleaseAsync(AudioSource audioSource)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(audioSource.clip.length * audioSource.pitch), cancellationToken: mainCancellationTokenSource.Token);
            if (mainCancellationTokenSource.IsCancellationRequested) return;
            audioSource.Stop();
            audioSource.time = 0;
            audioSourcePool.Release(audioSource);
        }

        private AudioSource GetDefaultAudioSourceForCategory(AudioCategory audioCategory)
        {
            switch (audioCategory)
            {
                case AudioCategory.UI:
                    return UiAudioSource;
                case AudioCategory.Chat:
                    return ChatAudioSource;
                case AudioCategory.Music:
                    return MusicAudioSource;
                case AudioCategory.World:
                case AudioCategory.Avatar:
                case AudioCategory.None:
                default: throw new ArgumentOutOfRangeException(nameof(audioCategory), audioCategory, null);
            }
        }

        private AudioSource GetAudioSourceFromPoolForCategory(AudioCategory audioCategory)
        {
            AudioSource audioSource = audioSourcePool.Get();
            AudioCategorySettings settings = audioSettings.GetSettingsForCategory(audioCategory);
            audioSource.priority = settings.AudioPriority;
            audioSource.outputAudioMixerGroup = settings.MixerGroup;
            audioSource.spatialBlend = 0;
            return audioSource;
        }

        private struct ContinuousPlaybackAudioData
        {
            public AudioSource AudioSource { get; }
            public Tweener FadeTweener { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; }

            public ContinuousPlaybackAudioData(AudioSource audioSource, Tweener fadeTweener, CancellationTokenSource cancellationTokenSource)
            {
                AudioSource = audioSource;
                FadeTweener = fadeTweener;
                CancellationTokenSource = cancellationTokenSource;
            }
        }
    }
}
