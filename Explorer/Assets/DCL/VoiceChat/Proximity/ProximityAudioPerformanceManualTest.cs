using LiveKit.Rooms.Streaming.Audio;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Manual performance testbed for proximity spatial audio.
    /// Attach to any GameObject, adjust source count at runtime via Inspector or on-screen slider.
    /// Creates real <see cref="LivekitAudioSource"/> instances with a playing test tone
    /// so that <c>OnAudioFilterRead</c> fires on the AudioThread.
    /// </summary>
    public class ProximityAudioPerformanceManualTest : MonoBehaviour
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

        [Header("GUI")]
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private Key toggleOverlayKey = Key.F9;

        private readonly List<LivekitAudioSource> activeSources = new (128);
        private AudioClip testClip;

        private ProfilerRecorder mainThreadRecorder;
        private ProfilerRecorder gcAllocRecorder;

        private float fps;
        private float smoothFps;

        private void Start()
        {
            testClip = CreateStereoTestClip();
            mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleOverlayKey].wasPressedThisFrame)
                showOverlay = !showOverlay;

            if (activeSources.Count != targetSourceCount)
                SyncSourceCount();

            UpdateSpatialSettings();

            fps = 1f / Time.unscaledDeltaTime;
            smoothFps = Mathf.Lerp(smoothFps, fps, Time.unscaledDeltaTime * 4f);
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

        private void OnGUI()
        {
            if (!showOverlay) return;

            GUILayout.BeginArea(new Rect(10, 10, 360, 280));

            var boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 14 };
            GUILayout.BeginVertical(boxStyle);

            GUILayout.Label("<b>Proximity Audio Perf Testbed</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15 });

            GUILayout.Space(4);
            GUILayout.Label($"Active Sources: {activeSources.Count}");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Count:", GUILayout.Width(50));
            targetSourceCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(targetSourceCount, 0, 200));
            GUILayout.Label(targetSourceCount.ToString(), GUILayout.Width(35));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label($"FPS: {smoothFps:F1}");

            if (mainThreadRecorder.Valid && mainThreadRecorder.Count > 0)
                GUILayout.Label($"Main Thread: {mainThreadRecorder.LastValue / 1_000_000.0:F2} ms");

            if (gcAllocRecorder.Valid && gcAllocRecorder.Count > 0)
                GUILayout.Label($"GC.Alloc: {gcAllocRecorder.LastValue} bytes");

            GUILayout.Space(4);
            enableSpatialization = GUILayout.Toggle(enableSpatialization, "Spatialization");
            smoothPanning = GUILayout.Toggle(smoothPanning, "Smooth Panning");

            GUILayout.BeginHorizontal();
            GUILayout.Label("ILD:", GUILayout.Width(30));
            ildStrength = GUILayout.HorizontalSlider(ildStrength, 0f, 1f);
            GUILayout.Label(ildStrength.ToString("F2"), GUILayout.Width(35));
            GUILayout.EndHorizontal();

            GUILayout.Label($"<i>Toggle overlay: {toggleOverlayKey}</i>", new GUIStyle(GUI.skin.label) { richText = true });

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            foreach (LivekitAudioSource source in activeSources)
                if (source != null) Destroy(source.gameObject);

            activeSources.Clear();
            mainThreadRecorder.Dispose();
            gcAllocRecorder.Dispose();
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
}
