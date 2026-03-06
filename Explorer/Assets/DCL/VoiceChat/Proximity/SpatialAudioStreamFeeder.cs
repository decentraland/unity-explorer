using System;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Feeds a LiveKit <see cref="AudioStream"/> into a mono streaming <see cref="AudioClip"/>
    /// so that Unity's native AudioSource processing (panStereo, spatialBlend, 3D settings)
    /// applies correctly.
    /// <para>
    /// <see cref="LivekitAudioSource"/> uses <c>OnAudioFilterRead</c> which sits AFTER the
    /// AudioSource's spatial/pan processing in Unity's DSP chain. Since the AudioSource has
    /// no clip, the spatial processing runs on silence and the raw LiveKit audio bypasses it.
    /// By feeding audio through a streaming clip instead, the data enters the pipeline BEFORE
    /// spatial processing and all AudioSource settings work as expected.
    /// </para>
    /// </summary>
    public class SpatialAudioStreamFeeder : MonoBehaviour
    {
        [Header("Debug — toggle in PlayMode to compare quality")]
        [Tooltip("ON = streaming AudioClip (new, full pipeline). OFF = OnAudioFilterRead (old, bypasses pan/effects).")]
        public bool UseStreamingClip = true;

        private Weak<AudioStream> stream;
        private AudioSource audioSource;
        private LivekitAudioSource livekitAudioSource;
        private int sampleRate;
        private bool activeMode;

        public void Initialize(Weak<AudioStream> audioStream, AudioSource source)
        {
            stream = audioStream;
            audioSource = source;
            livekitAudioSource = GetComponent<LivekitAudioSource>();
            sampleRate = AudioSettings.outputSampleRate;

            activeMode = UseStreamingClip;
            ApplyStreamingClipMode();
        }

        public void Free()
        {
            stream = Weak<AudioStream>.Null;
        }

        private void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        private void OnDestroy()
        {
            Free();
        }

        private void Update()
        {
            if (UseStreamingClip == activeMode) return;

            activeMode = UseStreamingClip;

            if (activeMode)
                ApplyStreamingClipMode();
            else
                ApplyOnAudioFilterReadMode();
        }

        private void ApplyStreamingClipMode()
        {
            if (livekitAudioSource != null)
                livekitAudioSource.Free();

            CreateAndAssignClip();

            if (audioSource != null)
                audioSource.Play();
        }

        private void ApplyOnAudioFilterReadMode()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }

            if (livekitAudioSource != null)
                livekitAudioSource.Construct(stream);

            if (audioSource != null)
                audioSource.Play();
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            int newSampleRate = AudioSettings.outputSampleRate;
            if (newSampleRate == sampleRate) return;

            sampleRate = newSampleRate;

            if (!activeMode) return;

            bool wasPlaying = audioSource != null && audioSource.isPlaying;
            CreateAndAssignClip();

            if (wasPlaying && audioSource != null)
                audioSource.Play();
        }

        private void CreateAndAssignClip()
        {
            var clip = AudioClip.Create(
                "SpatialLivekitStream",
                sampleRate,
                1,
                sampleRate,
                true,
                OnPCMRead
            );

            if (audioSource != null)
            {
                audioSource.clip = clip;
                audioSource.loop = true;
            }
        }

        private void OnPCMRead(float[] data)
        {
            Option<AudioStream> resource = stream.Resource;

            if (resource.Has)
            {
                resource.Value.ReadAudio(data.AsSpan(), 1, sampleRate);
            }
            else
            {
                Array.Fill(data, 0f);
            }
        }
    }
}
