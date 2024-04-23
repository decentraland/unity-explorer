using DCL.Diagnostics;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DCL.Audio
{
    public class WorldAudioPlaybackController : MonoBehaviour, IDisposable
    {
        [SerializeField]
        private WorldAudioSettings audioSettings;
        [SerializeField]
        private AudioSource audioSourcePrefab;

        private readonly Dictionary<int, List<AudioSource>> landscapeAudioSourcesPerIndex = new ();
        private readonly Dictionary<int, List<AudioSource>> oceanAudioSourcesPerIndex = new ();
        private GameObjectPool<AudioSource> audioSourcePool;

        public void Dispose()
        {
            WorldAudioEventsBus.Instance.PlayLandscapeAudioEvent -= OnPlayLandscapeAudioEvent;
            WorldAudioEventsBus.Instance.StopWorldAudioEvent -= StopAndReleaseAudioSources;
            audioSourcePool.Dispose();
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
            var clip = audioClipConfig.AudioClips[clipIndex];
            audioSource.clip = clip;
            audioSource.time = Random.Range(0, clip.length);
            audioSource.volume = audioClipConfig.RelativeVolume;
            audioSource.Play();
        }



        private void OnPlayLandscapeAudioEvent(int index, NativeArray<int2> audioSourcesPositions, WorldAudioClipType clipType)
        {
            AudioClipConfig audioClipConfig = audioSettings.GetAudioClipConfigForType(WorldAudioClipType.Landscape).DayClip; //We can switch this depending on the time of day for example

            if (!CheckAudioClips(audioClipConfig)) return;

            var audioSourcesPerIndex = landscapeAudioSourcesPerIndex;

            switch (clipType)
            {
                default:
                case WorldAudioClipType.Landscape:
                    break;
                case WorldAudioClipType.Ocean:
                    audioSourcesPerIndex = oceanAudioSourcesPerIndex;
                    break;

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
                    audioSourceList[i].transform.position = new Vector3(position.x, 1, position.y);;
                }
            }

            foreach (AudioSource audioSource in audioSourceList)
            {
                if (!audioSource.isPlaying) { SetupAudioClip(audioSource, audioClipConfig); }
            }
        }

        private void StopAndReleaseAudioSources(int index, WorldAudioClipType clipType)
        {
            var audioSourcesPerIndex = landscapeAudioSourcesPerIndex;

            switch (clipType)
            {
                default:
                case WorldAudioClipType.Landscape: break;
                case WorldAudioClipType.Ocean:
                    audioSourcesPerIndex = oceanAudioSourcesPerIndex;
                    break;
            }


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
