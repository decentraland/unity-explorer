using System;
using UnityEngine;
using UnityEngine.Assertions;
using Plugins.NativeAudioAnalysis;

namespace Plugins.NativeAudioAnalysis.Playground
{
    public class AudioAnalysisPlayground : MonoBehaviour
    {
        [SerializeField]
        private AnalysisResultMode analysisMode = AnalysisResultMode.Logarithmic;
        [SerializeField]
        private float amplitudeGain = 5;
        [SerializeField]
        private float bandsGain = 0.05f;
        [Space]
        [SerializeField] private AudioAnalysis medianAnalysis = default(AudioAnalysis);
        [SerializeField] private AudioAnalysis lastAnalysis = default(AudioAnalysis);
        [Space]
        [SerializeField]
        private GameObject[] bars = Array.Empty<GameObject>(); // Length is supposed to be equal to BANDS
        [Space]
        [SerializeField]
        private Transform amplitudeCenter = null!;
        [SerializeField]
        private float amplitudePower = 5;
        [Space]
        [SerializeField]
        private ParticleSystem particles = null!;
        [SerializeField]
        private int particlesCountPerBurst = 50;
        [Space]
        [SerializeField]
        private Transform bmpCenter = null!;
        [SerializeField]
        private float rotateByBpmSpeed = 10;

        private Camera mainCamera = null!;
        private int sampleRate;

        private ThreadSafeLastAudioFrameReadFilter lastAudioFrame = null!;

        private void Start() {
            Assert.AreEqual(bars.Length, NativeMethods.BANDS, "Bar count is not equls to band count");
            Assert.IsNotNull(amplitudeCenter, "Amplitude center is missing");
            Assert.IsNotNull(bmpCenter, "BPM center is missing");
            Assert.IsNotNull(particles, "Particles are missing");

            mainCamera = Camera.main;
            sampleRate = AudioSettings.outputSampleRate;

            Assert.IsTrue(TryGetComponent<AudioSource>(out AudioSource source), "AudioSource is not attached");
            Assert.IsNotNull(source.clip, "Clip is not selected");

            lastAudioFrame = GetComponent<ThreadSafeLastAudioFrameReadFilter>();
            Assert.IsNotNull(lastAudioFrame, "ThreadSafeLastAudioFrameReadFilter is not found");
        }

        private void Update() 
        {
            if (lastAudioFrame.TryConsume(out float[]? output, out int outChannels, out int outSampleRate))
            {
                lastAnalysis = NativeMethods.AnalyzeAudioBuffer(output!, outSampleRate, analysisMode, amplitudeGain, bandsGain);
            }

            // Bands intensity
            unsafe {
                fixed (float* ptr = lastAnalysis.bands)
                {
                    int iterations = Mathf.Min(bars.Length, NativeMethods.BANDS);
                    for (int i = 0; i < iterations; i++)
                    {
                        Transform bar = bars[i].transform;
                        Vector3 scale = bar.localScale;
                        scale.y = ptr[i];
                        bar.localScale = scale;
                    }
                }
            }

            // Amplitude
            amplitudeCenter.localScale = Vector3.one * lastAnalysis.amplitude * amplitudePower;

            /* Beyond MVP
            // Spectral centroid
            float t = Mathf.InverseLerp(200, 4000, lastAnalysis.spectral_centroid);
            mainCamera.backgroundColor = Color.Lerp(Color.red, Color.blue, t);

            // Onset
            if (lastAnalysis.onset) {
                print("Burst!");
                particles.Emit(particlesCountPerBurst); // Emit burst on set
            }

            // bpm
            bmpCenter.Rotate(0, rotateByBpmSpeed * Time.timeScale * lastAnalysis.bpm, 0);

            // median
            medianAnalysis.amplitude = (medianAnalysis.amplitude + lastAnalysis.amplitude) / 2;
            medianAnalysis.spectral_centroid = (medianAnalysis.spectral_centroid + lastAnalysis.spectral_centroid) / 2;
            medianAnalysis.spectral_flux = (medianAnalysis.spectral_flux + lastAnalysis.spectral_flux) / 2;
            medianAnalysis.bpm = (medianAnalysis.bpm + lastAnalysis.bpm) / 2;

            for (int i = 0; i < medianAnalysis.bands.Length; i++) 
            {
                medianAnalysis.bands[i] = (medianAnalysis.bands[i] + lastAnalysis.bands[i]) / 2;
            }
            */
        }
    }
}
