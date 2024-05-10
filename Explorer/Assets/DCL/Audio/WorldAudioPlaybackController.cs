using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using Random = UnityEngine.Random;

namespace DCL.Audio
{
    public class WorldAudioPlaybackController : MonoBehaviour, IDisposable
    {
        [SerializeField]
        private WorldAudioSettings audioSettings;
        [SerializeField]
        private AudioSource audioSourcePrefab;

        private readonly Dictionary<WorldAudioClipType, Dictionary<int, List<WorldPlaybackAudioData>>> audioDatasPerIndexDictionary = new ();
        private GameObjectPool<AudioSource> audioSourcePool;
        private CancellationTokenSource mainCancellationTokenSource;

        public void Dispose()
        {
            audioDatasPerIndexDictionary.Clear();
            audioSourcePool?.Dispose();
            mainCancellationTokenSource.SafeCancelAndDispose();
        }

        public void Initialize()
        {
            audioSourcePool = new GameObjectPool<AudioSource>(transform, OnCreateAudioSource);
        }

        private AudioSource OnCreateAudioSource()
        {
            AudioSource audioSource = Instantiate(audioSourcePrefab, transform);
            audioSource.outputAudioMixerGroup = audioSettings.MixerGroup;
            audioSource.spatialBlend = 1;
            return audioSource;
        }

        private AudioSource GetAndSetAudioSourceAtPosition(int2 position)
        {
            AudioSource audioSource = audioSourcePool.Get();
            Transform audioSourceTransform = audioSource.transform;
            audioSourceTransform.parent = transform;
            audioSourceTransform.position = new Vector3(position.x, 1, position.y);
            return audioSource;
        }

        private void SetupAudioClip(WorldPlaybackAudioData audioData, AudioClipConfig audioClipConfig)
        {
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(audioClipConfig);
            AudioClip clip = audioClipConfig.AudioClips[clipIndex];
            audioData.AudioSource.clip = clip;
            audioData.AudioSource.time = Random.Range(0, clip.length);
            audioData.AudioSource.volume = audioClipConfig.RelativeVolume;
            audioData.AudioSource.Play();

            //if there is only one clip, we just loop it, otherwise, we schedule to start after the first one has finished.
            if (audioClipConfig.AudioClips.Length == 1)
            {
                audioData.AudioSource.loop = true;
                return;
            }

            float remainingTime = clip.length - audioData.AudioSource.time;
            AudioPlaybackUtilities.SchedulePlaySoundAsync(audioData.CancellationTokenSource.Token, audioClipConfig, remainingTime, audioData.AudioSource).Forget();
        }

        public void ReleaseAudioSourcesFromTerrain(int index, WorldAudioClipType clipType)
        {
            StopAndReleaseAudioSources(index, clipType);
        }

        public void SetupAudioSourcesOnTerrain(int index, NativeArray<int2> audioSourcesPositions, WorldAudioClipType clipType)
        {
            AudioClipConfig audioClipConfig = audioSettings.GetAudioClipConfigForType(clipType).DayClip; //We can switch this depending on the time of day for example

            if (!CheckAudioClips(audioClipConfig)) return;

            if (!audioDatasPerIndexDictionary.TryGetValue(clipType, out Dictionary<int, List<WorldPlaybackAudioData>> audioSourcesPerIndex))
            {
                audioSourcesPerIndex = new Dictionary<int, List<WorldPlaybackAudioData>>();
                audioDatasPerIndexDictionary.Add(clipType, audioSourcesPerIndex);
            }

            if (!audioSourcesPerIndex.TryGetValue(index, out List<WorldPlaybackAudioData> audioSourceList))
            {
                audioSourceList = new List<WorldPlaybackAudioData>();

                foreach (int2 position in audioSourcesPositions)
                {
                    AudioSource audioSource = GetAndSetAudioSourceAtPosition(position);
                    var cts = new CancellationTokenSource();
                    var audioData = new WorldPlaybackAudioData(audioSource, CancellationTokenSource.CreateLinkedTokenSource(cts.Token, mainCancellationTokenSource.Token));
                    audioSourceList.Add(audioData);
                }

                audioSourcesPerIndex.Add(index, audioSourceList);
            }
            else
            {
                for (var i = 0; i < Math.Min(audioSourcesPositions.Length, audioSourcesPositions.Length); i++)
                {
                    int2 position = audioSourcesPositions[i];
                    audioSourceList[i].AudioSource.transform.position = new Vector3(position.x, 1, position.y);
                }
            }

            foreach (WorldPlaybackAudioData audioData in audioSourceList)
            {
                if (!audioData.AudioSource.isPlaying) SetupAudioClip(audioData, audioClipConfig);
            }
        }

        private void StopAndReleaseAudioSources(int index, WorldAudioClipType clipType)
        {
            Dictionary<int, List<WorldPlaybackAudioData>> audioSourcesPerIndex = GetAudioDataPerIndexForClipType(clipType);

            if (!audioSourcesPerIndex.TryGetValue(index, out List<WorldPlaybackAudioData> audioSourceList))
            {
                ReportHub.Log(new ReportData(ReportCategory.AUDIO), $"Cannot Release AudioSources for terrain of type {clipType} with Index {index} as there is none in the dictionary");
                return;
            }

            foreach (WorldPlaybackAudioData audioData in audioSourceList)
            {
                audioData.AudioSource.Stop();
                audioData.CancellationTokenSource.Cancel();
                audioSourcePool.Release(audioData.AudioSource);
            }

            audioSourcesPerIndex.Remove(index);
        }

        private Dictionary<int, List<WorldPlaybackAudioData>> GetAudioDataPerIndexForClipType(WorldAudioClipType clipType)
        {
            if (!audioDatasPerIndexDictionary.TryGetValue(clipType, out Dictionary<int, List<WorldPlaybackAudioData>> audioDatasPerIndex))
            {
                audioDatasPerIndex = new Dictionary<int, List<WorldPlaybackAudioData>>();
                audioDatasPerIndexDictionary.Add(clipType, audioDatasPerIndex);
            }

            return audioDatasPerIndex;
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

        private struct WorldPlaybackAudioData
        {
            public AudioSource AudioSource { get; }
            public CancellationTokenSource CancellationTokenSource { get; }

            public WorldPlaybackAudioData(AudioSource audioSource, CancellationTokenSource cancellationTokenSource)
            {
                AudioSource = audioSource;
                CancellationTokenSource = cancellationTokenSource;
            }
        }
    }
}
