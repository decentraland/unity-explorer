using LiveKit.Rooms.Streaming.Audio;
using RichTypes;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Manual performance testbed for nearby spatial audio with real panning.
    /// Creates real <see cref="LivekitAudioSource"/> instances with an injected fake
    /// <c>AudioStream</c> so that <c>ApplySpatialPanning</c> executes on the AudioThread.
    /// Use the Profiler to observe <c>LiveKit.Spatial.ILD.EqualPower</c> marker on Audio Mixer Thread.
    /// </summary>
    public class NearbyAudioPerformanceManualTest : MonoBehaviour
    {
        [Header("Sources")]
        [Range(0, 200)]
        [SerializeField] private int targetSourceCount = 10;
        [SerializeField] private float spreadRadius = 15f;

        [Header("Spatial Settings")]
        [SerializeField] private bool enableSpatialization = true;
        [Range(0f, 1f)]
        [SerializeField] private float ildStrength = 0.75f;
        [SerializeField] private bool smoothPanning;

        [Header("Audio")]
        [SerializeField] private bool playTestTone = true;
        [Range(0f, 0.1f)]
        [SerializeField] private float testToneVolume;

        private readonly List<LivekitAudioSource> activeSources = new (128);
        private AudioClip testClip;

        private void Start()
        {
            testClip = CreateStereoTestClip();
        }

        private void Update()
        {
            if (activeSources.Count != targetSourceCount)
                SyncSourceCount();

            UpdateSpatialSettings();
        }

        private void SyncSourceCount()
        {
            while (activeSources.Count > targetSourceCount)
            {
                int last = activeSources.Count - 1;
                LivekitAudioSource source = activeSources[last];
                activeSources.RemoveAt(last);

                if (source != null)
                    Destroy(source.gameObject);
            }

            while (activeSources.Count < targetSourceCount)
                activeSources.Add(CreateSource());
        }

        private LivekitAudioSource CreateSource()
        {
            LivekitAudioSource source = LivekitAudioSource.New(isSpatial: enableSpatialization);
            source.SetSpatialSettings(enableSpatialization, ildStrength, smoothPanning);
            source.transform.position = transform.position + Random.insideUnitSphere * spreadRadius;
            source.SetSpatialAngles(Random.Range(-Mathf.PI, Mathf.PI), Random.Range(-Mathf.PI * 0.5f, Mathf.PI * 0.5f));

            InjectFakeAudioStream(source);

            if (playTestTone && testClip != null)
            {
                source.AudioSource.clip = testClip;
                source.AudioSource.loop = true;
                source.AudioSource.volume = testToneVolume;
                source.Play();
            }

            return source;
        }

        private void UpdateSpatialSettings()
        {
            Vector3 listenerPos = transform.position;

            foreach (LivekitAudioSource source in activeSources)
            {
                source.SetSpatialSettings(enableSpatialization, ildStrength, smoothPanning);

                if (source.AudioSource.volume != testToneVolume)
                    source.AudioSource.volume = testToneVolume;

                Vector3 dir = source.transform.position - listenerPos;
                float azimuth = Mathf.Atan2(dir.x, dir.z);
                float horizontalDist = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
                float elevation = Mathf.Atan2(dir.y, horizontalDist);
                source.SetSpatialAngles(azimuth, elevation);
            }
        }

        private void OnDestroy()
        {
            foreach (LivekitAudioSource source in activeSources)
                if (source != null) Destroy(source.gameObject);

            activeSources.Clear();
        }

        /// <summary>
        /// Injects a fake <c>AudioStream</c> that passes <c>stream.Resource.Has</c> check
        /// but early-returns in <c>ReadAudio</c> (disposed internal), allowing
        /// <c>ApplySpatialPanning</c> to execute on the AudioThread without FFI.
        /// </summary>
        private static void InjectFakeAudioStream(LivekitAudioSource source)
        {
            var fakeInternal = (AudioStreamInternal)FormatterServices.GetUninitializedObject(typeof(AudioStreamInternal));
            typeof(AudioStreamInternal)
               .GetField("disposed", BindingFlags.NonPublic | BindingFlags.Instance)!
               .SetValue(fakeInternal, true);

            var fakeStream = (AudioStream)FormatterServices.GetUninitializedObject(typeof(AudioStream));
            typeof(AudioStream)
               .GetField("currentInternal", BindingFlags.NonPublic | BindingFlags.Instance)!
               .SetValue(fakeStream, fakeInternal);
            typeof(AudioStream)
               .GetField("currentChannels", BindingFlags.NonPublic | BindingFlags.Instance)!
               .SetValue(fakeStream, (uint)2);
            typeof(AudioStream)
               .GetField("currentSampleRate", BindingFlags.NonPublic | BindingFlags.Instance)!
               .SetValue(fakeStream, (uint)AudioSettings.outputSampleRate);

            var owned = new Owned<AudioStream>(fakeStream);
            source.Construct(owned.Downgrade());
        }

        private static AudioClip CreateStereoTestClip()
        {
            const int SAMPLE_RATE = 44100;
            const int CHANNELS = 2;
            var clip = AudioClip.Create("PerfTestTone", SAMPLE_RATE, CHANNELS, SAMPLE_RATE, false);
            float[] samples = new float[SAMPLE_RATE * CHANNELS];

            for (int i = 0; i < samples.Length; i += CHANNELS)
            {
                float t = (float)(i / CHANNELS) / SAMPLE_RATE;
                float val = Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.1f;
                samples[i] = val;
                samples[i + 1] = val;
            }

            clip.SetData(samples, 0);
            return clip;
        }
    }

#if UNITY_EDITOR
    public static class NearbyAudioPerformanceManualTestMenu
    {
        [UnityEditor.MenuItem("Decentraland/Manual Tests/ Nearby Audio Panning [Perf]")]
        private static void OpenTestbed()
        {
            if (UnityEditor.EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play mode first.");
                return;
            }

            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var go = new GameObject("NearbyAudioPerfTestbed");
            go.AddComponent<NearbyAudioPerformanceManualTest>();

            UnityEditor.Selection.activeGameObject = go;
            Debug.Log("Nearby Audio Panning Testbed ready — press Play.");
        }
    }
#endif
}
