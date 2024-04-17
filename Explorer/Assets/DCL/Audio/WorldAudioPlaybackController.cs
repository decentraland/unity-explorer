using DCL.Diagnostics;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Audio
{
    public class WorldAudioPlaybackController : MonoBehaviour, IDisposable
    {
        [SerializeField]
        private WorldAudioSettings audioSettings;

        private Dictionary<int, List<AudioSource>> audioSourcesPerIndex = new Dictionary<int, List<AudioSource>>();
        private GameObjectPool<AudioSource> audioSourcePool;
        public void Dispose()
        {
            WorldAudioEventsBus.Instance.PlayLandscapeAudioEvent -= OnPlayLandscapeAudioEvent;
            WorldAudioEventsBus.Instance.StopLandscapeAudioEvent -= StopAndReleaseAudioSources;
            audioSourcePool.Dispose();
        }

        public void Initialize()
        {
            WorldAudioEventsBus.Instance.PlayLandscapeAudioEvent += OnPlayLandscapeAudioEvent;
            WorldAudioEventsBus.Instance.StopLandscapeAudioEvent += StopAndReleaseAudioSources;
            audioSourcePool = new GameObjectPool<AudioSource>(this.transform);
        }

        private void OnPlayLandscapeAudioEvent(int index, NativeArray<int2> audioSourcesPositions)
        {
            if (!audioSourcesPerIndex.TryGetValue(index, out var audioSourceList))
            {
                if (audioSourcesPositions.Length == 0) return;

                audioSourceList = new List<AudioSource>();

                foreach (var position in audioSourcesPositions)
                {
                    var audioSource = audioSourcePool.Get();
                    audioSource.gameObject.SetActive(true);
                    audioSource.loop = true; //for now
                    audioSource.spatialBlend = 1;
                    audioSource.outputAudioMixerGroup = audioSettings.MixerGroup;
                    audioSource.transform.parent = this.transform;
                    audioSource.transform.position = new Vector3(position.x, 1, position.y);
                    audioSourceList.Add(audioSource);
                }

                audioSourcesPerIndex.Add(index, audioSourceList);

                var audioClipConfig = audioSettings.GetAudioClipConfigForType(WorldAudioSettings.WorldAudioClipType.GladeDay); //We can check time of day and switch this
                //We might want to check the clip before doing all this as well
                if (CheckAudioClips(audioClipConfig))
                {
                    foreach (var audioSource in audioSourceList)
                    {
                        int clipIndex = AudioPlaybackUtilities.GetClipIndex(audioClipConfig);
                        audioSource.clip = audioClipConfig.AudioClips[clipIndex];
                        audioSource.time = Random.Range(0, audioSource.clip.length);
                        audioSource.Play();
                    }
                }
            }
        }

        private void StopAndReleaseAudioSources(int index)
        {
            if (audioSourcesPerIndex.TryGetValue(index, out var audioSourceList))
            {
                foreach (var audioSource in audioSourceList)
                {
                    audioSource.Stop();
                    audioSourcePool.Release(audioSource);
                }
                audioSourcesPerIndex.Remove(index);
            }
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
