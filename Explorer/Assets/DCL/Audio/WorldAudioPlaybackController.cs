using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Audio
{
    public class WorldAudioPlaybackController : MonoBehaviour, IDisposable
    {
        [SerializeField]
        private WorldAudioSettings audioSettings;

        private Dictionary<int, AudioSource> audioSourcesPerIndex = new Dictionary<int, AudioSource>();
        private GameObjectPool<AudioSource> audioSourcePool;
        public void Dispose()
        {
            WorldAudioEventsBus.Instance.PlayLoopingUIAudioEvent -= OnPlayLoopingUIAudioEvent;
            audioSourcePool.Dispose();
        }

        public void Initialize()
        {
            WorldAudioEventsBus.Instance.PlayLoopingUIAudioEvent += OnPlayLoopingUIAudioEvent;
            audioSourcePool = new GameObjectPool<AudioSource>(this.transform);
        }

        private void OnPlayLoopingUIAudioEvent(int index, float volume, bool play)
        {

            if (!audioSourcesPerIndex.TryGetValue(index, out var audioSource))
            {
                if (!play || volume < audioSettings.MinVolume)
                {
                    return;
                }

                audioSource = audioSourcePool.Get();
                audioSource.gameObject.SetActive(true);
                audioSource.loop = true; //for now
                audioSource.outputAudioMixerGroup = audioSettings.MixerGroup;
                audioSourcesPerIndex.Add(index, audioSource);
            }


            if (!play || volume < audioSettings.MinVolume)
            {
                StopAndReleaseAudioSource(audioSource, index);
                return;
            }

            audioSource.volume = volume;

            if (!audioSource.isPlaying)
            {
                var audioClipConfig = audioSettings.GetAudioClipConfigForType(WorldAudioSettings.WorldAudioClipType.GladeDay); //We can check time of day and switch this
                if (CheckAudioClips(audioClipConfig))
                {
                    int clipIndex = AudioPlaybackUtilities.GetClipIndex(audioClipConfig);
                    audioSource.clip = audioClipConfig.AudioClips[clipIndex];
                    audioSource.time = Random.Range(0, audioSource.clip.length);
                    audioSource.Play();
                }
            }
        }

        private void StopAndReleaseAudioSource(AudioSource audioSource, int index)
        {
            audioSource.Stop();
            audioSourcesPerIndex.Remove(index);
            audioSourcePool.Release(audioSource);
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
    }
}
