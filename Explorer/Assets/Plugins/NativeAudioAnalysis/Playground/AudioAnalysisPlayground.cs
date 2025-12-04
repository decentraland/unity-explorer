using System;
using UnityEngine;
using UnityEngine.Assertions;
using Plugins.NativeAudioAnalysis;

namespace Plugins.NativeAudioAnalysis.Playground
{
    public class AudioAnalysisPlayground : MonoBehaviour
    {
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

        private bool enableAnalysis;

        private void Start() {
            Assert.AreEqual(bars.Length, NativeMethods.BANDS, "Bar count is not equls to band count");
            Assert.IsNotNull(amplitudeCenter, "Amplitude center is missing");
            Assert.IsNotNull(bmpCenter, "BPM center is missing");
            Assert.IsNotNull(particles, "Particles are missing");

            mainCamera = Camera.main;
            sampleRate = AudioSettings.outputSampleRate;
            enableAnalysis = true;
            medianAnalysis.bands = new float[NativeMethods.BANDS];

            Assert.IsTrue(TryGetComponent<AudioSource>(out AudioSource source), "AudioSource is not attached");
            Assert.IsNotNull(source.clip, "Clip is not selected");
        }

        private void Update() {

            // Bands intensity
            int iterations = Mathf.Min(bars.Length, lastAnalysis.bands.Length);
            for (int i = 0; i < iterations; i++)
            {
                Transform bar = bars[i].transform;
                Vector3 scale = bar.localScale;
                scale.y = lastAnalysis.bands[i];
                bar.localScale = scale;
            }

            // Amplitude
            amplitudeCenter.localScale = Vector3.one * lastAnalysis.amplitude * amplitudePower;

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
        }

        private void OnDisable()
        {
            enableAnalysis = false;
        }

        private void OnAudioFilterRead(float[] data, int channels) 
        {
            // It's ok for this case, possibility of race condition is acceptable in this case
            // Couple of frames won't bring big impact and mutex sync is not required
            if (enableAnalysis) 
            {
                lastAnalysis = NativeMethods.AnalyzeAudioBuffer(data, sampleRate);
            }
        }
    }
}
