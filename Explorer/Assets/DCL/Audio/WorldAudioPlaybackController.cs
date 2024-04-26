using DCL.Diagnostics;
using DCL.Optimization.Pools;
using JetBrains.Annotations;
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
        [SerializeField]
        private AudioSource audioSourcePrefab;

        private readonly Dictionary<WorldAudioClipType, Dictionary<int, List<AudioSource>>> audioSourcesPerIndexDictionary = new ();
        [CanBeNull] private GameObjectPool<AudioSource> audioSourcePool;

        public void Dispose()
        {
            WorldAudioEventsBus.Instance.PlayLandscapeAudioEvent -= OnPlayLandscapeAudioEvent;
            WorldAudioEventsBus.Instance.StopWorldAudioEvent -= StopAndReleaseAudioSources;
            audioSourcesPerIndexDictionary.Clear();
            audioSourcePool?.Dispose();
        }

        public void Initialize()
        {
            WorldAudioEventsBus.Instance.PlayLandscapeAudioEvent += OnPlayLandscapeAudioEvent;
            WorldAudioEventsBus.Instance.StopWorldAudioEvent += StopAndReleaseAudioSources;
            audioSourcePool = new GameObjectPool<AudioSource>(transform, OnCreateAudioSource);
        }

        private AudioSource OnCreateAudioSource()
        {
            AudioSource audioSource = Instantiate(audioSourcePrefab, transform);
            audioSource.outputAudioMixerGroup = audioSettings.MixerGroup;
            audioSource.loop = true;
            audioSource.spatialBlend = 1;
            return audioSource;
        }

        private AudioSource GetAudioSource(int2 position)
        {
            AudioSource audioSource = audioSourcePool.Get();
            Transform audioSourceTransform = audioSource.transform;
            audioSourceTransform.parent = transform;
            audioSourceTransform.position = new Vector3(position.x, 1, position.y);
            return audioSource;
        }

        private void SetupAudioClip(AudioSource audioSource, AudioClipConfig audioClipConfig)
        {
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(audioClipConfig);
            AudioClip clip = audioClipConfig.AudioClips[clipIndex];
            audioSource.clip = clip;
            audioSource.time = Random.Range(0, clip.length);
            audioSource.volume = audioClipConfig.RelativeVolume;
            audioSource.loop = true;
            audioSource.Play();
        }

        private void OnPlayLandscapeAudioEvent(int index, NativeArray<int2> audioSourcesPositions, WorldAudioClipType clipType)
        {
            AudioClipConfig audioClipConfig = audioSettings.GetAudioClipConfigForType(clipType).DayClip; //We can switch this depending on the time of day for example

            if (!CheckAudioClips(audioClipConfig)) return;

            if (!audioSourcesPerIndexDictionary.TryGetValue(clipType, out Dictionary<int, List<AudioSource>> audioSourcesPerIndex))
            {
                audioSourcesPerIndex = new Dictionary<int, List<AudioSource>>();
                audioSourcesPerIndexDictionary.Add(clipType, audioSourcesPerIndex);
            }

            if (!audioSourcesPerIndex.TryGetValue(index, out List<AudioSource> audioSourceList))
            {
                audioSourceList = new List<AudioSource>();

                foreach (int2 position in audioSourcesPositions)
                {
                    AudioSource audioSource = GetAudioSource(position);
                    audioSourceList.Add(audioSource);
                }

                audioSourcesPerIndex.Add(index, audioSourceList);
            }
            else
            {
                for (var i = 0; i < Math.Min(audioSourcesPositions.Length, audioSourcesPositions.Length); i++)
                {
                    int2 position = audioSourcesPositions[i];
                    audioSourceList[i].transform.position = new Vector3(position.x, 1, position.y);
                    ;
                }
            }

            foreach (AudioSource audioSource in audioSourceList)
            {
                if (!audioSource.isPlaying) { SetupAudioClip(audioSource, audioClipConfig); }
            }
        }

        private void StopAndReleaseAudioSources(int index, WorldAudioClipType clipType)
        {
            Dictionary<int, List<AudioSource>> audioSourcesPerIndex = GetAudioSourcePerIndexForClipType(clipType);

            if (!audioSourcesPerIndex.TryGetValue(index, out List<AudioSource> audioSourceList))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.AUDIO), $"Cannot Release AudioSources for terrain of type {clipType} with Index {index} as there is none in the dictionary");
                return;
            }

            foreach (AudioSource audioSource in audioSourceList)
            {
                audioSource.Stop();
                audioSourcePool.Release(audioSource);
            }

            audioSourcesPerIndex.Remove(index);
        }

        private Dictionary<int, List<AudioSource>> GetAudioSourcePerIndexForClipType(WorldAudioClipType clipType)
        {
            if (!audioSourcesPerIndexDictionary.TryGetValue(clipType, out Dictionary<int, List<AudioSource>> audioSourcesPerIndex))
            {
                audioSourcesPerIndex = new Dictionary<int, List<AudioSource>>();
                audioSourcesPerIndexDictionary.Add(clipType, audioSourcesPerIndex);
            }

            return audioSourcesPerIndex;
        }

        private bool CheckAudioClips(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig.AudioClips.Length == 0)
            {
                ReportHub.LogError(new ReportData(ReportCategory.AUDIO), $"Cannot Play Audio {audioClipConfig.name} as it has no Audio Clips Assigned");
                return false;
            }

            return true;
        }
    }
}
