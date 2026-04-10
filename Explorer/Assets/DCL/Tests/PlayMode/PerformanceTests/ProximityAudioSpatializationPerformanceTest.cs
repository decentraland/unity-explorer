using LiveKit.Rooms.Streaming.Audio;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    /// Benchmarks spatial audio processing for Proximity Voice Chat.
    ///
    /// <b>ApplySpatialPanningForNSources</b> — calls the REAL <c>LivekitAudioSource.ApplySpatialPanning</c>
    /// via reflection on real instances. Measures the ILD equal-power panning cost that runs on AudioThread.
    ///
    /// <b>AudioThreadCallbackOverhead</b> — creates N playing <c>LivekitAudioSource</c> instances and
    /// measures aggregate frame overhead from Unity's audio pipeline (OnAudioFilterRead callbacks).
    ///
    /// We use reflection for <c>ApplySpatialPanning</c> because <c>OnAudioFilterRead</c> requires
    /// a live <c>AudioStream</c> from a LiveKit room connection to reach the panning code path.
    /// </summary>
    public class ProximityAudioSpatializationPerformanceTest
    {
        private static readonly MethodInfo APPLY_SPATIAL_PANNING = typeof(LivekitAudioSource)
            .GetMethod("ApplySpatialPanning", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly List<LivekitAudioSource> sources = new (128);

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Assert.That(APPLY_SPATIAL_PANNING, Is.Not.Null,
                "ApplySpatialPanning not found — LiveKit SDK API may have changed");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (LivekitAudioSource source in sources)
                if (source != null) Object.DestroyImmediate(source.gameObject);

            sources.Clear();
        }

        /// <summary>
        /// Calls the real <c>LivekitAudioSource.ApplySpatialPanning</c> on N real instances.
        /// Each instance is configured via public API (SetSpatialSettings / SetSpatialAngles).
        /// Buffer is pre-filled to simulate ReadAudio output.
        /// </summary>
        [Test]
        [Performance]
        [TestCase(1, 512, TestName = "Panning_1_Source_512")]
        [TestCase(1, 1024, TestName = "Panning_1_Source_1024")]
        [TestCase(10, 1024, TestName = "Panning_10_Sources_1024")]
        [TestCase(50, 1024, TestName = "Panning_50_Sources_1024")]
        [TestCase(100, 1024, TestName = "Panning_100_Sources_1024")]
        public void ApplySpatialPanningForNSources(int sourceCount, int bufferSize)
        {
            const int CHANNELS = 2;
            int totalSamples = bufferSize * CHANNELS;
            float[] audioBuffer = new float[totalSamples];

            SetupSources(sourceCount);

            var invokeArgs = new object[] { audioBuffer, CHANNELS };

            ProfilerRecorder gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            Measure
               .Method(() =>
                {
                    for (int s = 0; s < sourceCount; s++)
                    {
                        for (int i = 0; i < totalSamples; i++)
                            audioBuffer[i] = 0.5f;

                        APPLY_SPATIAL_PANNING.Invoke(sources[s], invokeArgs);
                    }
                })
               .WarmupCount(10)
               .MeasurementCount(50)
               .GC()
               .Run();

            Debug.Log($"[ApplySpatialPanning] {sourceCount} sources × {bufferSize} samples — GC.Alloc last: {gcAlloc.LastValue} bytes");
            gcAlloc.Dispose();
        }

        /// <summary>
        /// Creates N <c>LivekitAudioSource</c> instances with a playing AudioClip to trigger
        /// <c>OnAudioFilterRead</c> on Unity's AudioThread.
        /// Without a live AudioStream the panning itself won't execute, but this measures
        /// the real callback scheduling + null-check overhead per source per audio buffer.
        /// Compare across source counts to see scaling behavior.
        /// </summary>
        [UnityTest]
        [Performance]
        [TestCase(0, ExpectedResult = null, TestName = "AudioThread_Baseline_0")]
        [TestCase(10, ExpectedResult = null, TestName = "AudioThread_10_Sources")]
        [TestCase(50, ExpectedResult = null, TestName = "AudioThread_50_Sources")]
        [TestCase(100, ExpectedResult = null, TestName = "AudioThread_100_Sources")]
        public IEnumerator AudioThreadCallbackOverhead(int sourceCount)
        {
            AudioClip testClip = CreateStereoTestClip();

            for (int i = 0; i < sourceCount; i++)
            {
                LivekitAudioSource source = LivekitAudioSource.New(isSpatial: true);
                source.SetSpatialSettings(true, 0.75f, false);
                source.SetSpatialAngles(
                    Random.Range(-Mathf.PI, Mathf.PI),
                    Random.Range(-Mathf.PI * 0.5f, Mathf.PI * 0.5f));

                source.AudioSource.clip = testClip;
                source.AudioSource.loop = true;
                source.AudioSource.volume = 0f;
                source.Play();
                sources.Add(source);
            }

            // Let audio pipeline stabilize
            yield return new WaitForSeconds(0.5f);

            yield return Measure.Frames()
               .WarmupCount(30)
               .MeasurementCount(120)
               .Run();
        }

        private void SetupSources(int count)
        {
            Random.InitState(42);

            for (int i = 0; i < count; i++)
            {
                LivekitAudioSource source = LivekitAudioSource.New(isSpatial: true);
                source.SetSpatialSettings(true, 0.75f, false);
                source.SetSpatialAngles(
                    Random.Range(-Mathf.PI, Mathf.PI),
                    Random.Range(-Mathf.PI * 0.5f, Mathf.PI * 0.5f));
                sources.Add(source);
            }
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
