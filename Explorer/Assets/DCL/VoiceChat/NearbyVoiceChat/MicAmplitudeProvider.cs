using LiveKit.Audio;
using LiveKit.Scripts.Audio;
using System;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace DCL.VoiceChat
{
    public class MicAmplitudeProvider : IDisposable
    {
        private static readonly FieldInfo DEVICE_MIC_FIELD =
            typeof(MicrophoneRtcAudioSource).GetField(
                "deviceMicrophoneAudioSource",
                BindingFlags.NonPublic | BindingFlags.Instance)!;

        private MicrophoneAudioFilter? audioFilter;
        private float amplitude;

        public float Amplitude => Interlocked.CompareExchange(ref amplitude, 0f, 0f);

        public void Bind(MicrophoneRtcAudioSource rtcSource)
        {
            Unbind();

            audioFilter = DEVICE_MIC_FIELD.GetValue(rtcSource) as MicrophoneAudioFilter;

            if (audioFilter == null || !audioFilter.IsValid)
            {
                Debug.LogWarning("[MicAmplitudeProvider] Could not obtain MicrophoneAudioFilter via reflection");
                return;
            }

            audioFilter.AudioRead += OnAudioRead;
        }

        public void Unbind()
        {
            if (audioFilter is { IsValid: true })
                audioFilter.AudioRead -= OnAudioRead;

            audioFilter = null;
            Interlocked.Exchange(ref amplitude, 0f);
        }

        private void OnAudioRead(Span<float> data, int channels, int sampleRate)
        {
            float sum = 0f;

            for (int i = 0; i < data.Length; i++)
                sum += data[i] * data[i];

            float rms = Mathf.Sqrt(sum / data.Length);
            Interlocked.Exchange(ref amplitude, rms);
        }

        public void Dispose()
        {
            Unbind();
        }
    }
}
