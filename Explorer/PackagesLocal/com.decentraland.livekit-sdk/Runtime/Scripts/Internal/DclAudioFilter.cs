using System;
using UnityEngine;

namespace LiveKit
{
    // from https://github.com/Unity-Technologies/com.unity.webrtc
    //
    // DCL/Linux-port: renamed from AudioFilter → DclAudioFilter to avoid the
    // simple-name collision with built-in UnityEngine.AudioFilter that produces
    // a startup error in the editor:
    //   "Script 'AudioFilter' has the same name as built-in Unity component.
    //    AddComponent and GetComponent will not work with this script."
    // Unity's name-collision check uses simple class name, not the fully
    // qualified one, so the LiveKit. namespace prefix doesn't help. The rename
    // is contained: the only callers are inside client-sdk-unity itself
    // (no Explorer-side code references this type by simple name).
    public class DclAudioFilter : MonoBehaviour, IAudioFilter
    {
        [SerializeField] private bool silenceAfterCapture;
        
        private int _sampleRate;

        private void OnEnable()
        {
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        private void OnDestroy()
        {
            AudioRead = null;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            Span<float> span = data.AsSpan();
            // Called by Unity on the Audio thread
            AudioRead?.Invoke(span, channels, _sampleRate);
            if (silenceAfterCapture)
            {
                span.Clear();
            }
        }

        public void EnableSilenceAfterCapture()
        {
            silenceAfterCapture = true;
        }

        // Event is called from the Unity audio thread
        public event IAudioFilter.OnAudioDelegate? AudioRead;

        /// <summary>
        ///     Gets whether this audio filter is valid and can be used
        /// </summary>
        public bool IsValid => this != null;

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            _sampleRate = AudioSettings.outputSampleRate;
        }


        #if UNITY_EDITOR
        [ContextMenu(nameof(StartSource))]
        public void StartSource()
        {
            GetComponent<AudioSource>().Play();
        }

        [ContextMenu(nameof(StopSource))]
        public void StopSource()
        {
            GetComponent<AudioSource>().Stop();
        }

        [ContextMenu(nameof(StopSource))]
        public void LogInfo()
        {
            var source = GetComponent<AudioSource>();
            Debug.Log($"{nameof(DclAudioFilter)} Source: IsValid - {IsValid}, IsRecording - {source!.isPlaying}");
        }
        #endif
    }
}