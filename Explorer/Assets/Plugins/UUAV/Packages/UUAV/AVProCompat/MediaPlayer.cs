using System;
using UnityEngine;

namespace UUAV.Compat
{
    // AVPro-shaped facade over UUAVPlayer. Poll-based: exposes state through
    // Control / Info / TextureProducer and no events.
    [RequireComponent(typeof(AudioSource))]
    public sealed class MediaPlayer : MonoBehaviour
    {
        private UUAVPlayer uuavPlayer = null!;
        private UUAVBackend backend = null!;

        public IMediaControl Control => backend;
        public IMediaInfo Info => backend;
        public ITextureProducer TextureProducer => backend;

        public AudioSource AudioSource => uuavPlayer.AudioSource;

        public bool MediaOpened =>
            uuavPlayer is not null && uuavPlayer.State is not (UUAVState.Closed or UUAVState.Error or UUAVState.Unknown);

        public float AudioVolume
        {
            get => AudioSource ? AudioSource.volume : 0f;
            set
            {
                if (AudioSource)
                {
                    AudioSource.volume = value;
                }
            }
        }

        private void Awake()
        {
            uuavPlayer = gameObject.AddComponent<UUAVPlayer>();
            backend = new UUAVBackend(uuavPlayer);
        }

        private void Update()
        {
            backend.Tick();
        }

#if UNITY_INCLUDE_TESTS
        // EditMode never awakes components: tests run it by hand so the facade owns a UUAVPlayer.
        public void AwakeForTests() => Awake();
#endif

        public bool OpenMedia(string path, bool autoPlay)
        {
            uuavPlayer.OpenMedia(path);

            if (autoPlay)
            {
                uuavPlayer.Play();
            }

            return true;
        }

        public void CloseMedia()
        {
            uuavPlayer.CloseMedia();
        }

        /// <summary>
        /// Opt-in snapshot of the player's DSP output for the main thread; see <see cref="UUAVPlayer.SetAudioCaptureEnabled"/>.
        /// </summary>
        public void SetAudioCaptureEnabled(bool enabled)
        {
            uuavPlayer.SetAudioCaptureEnabled(enabled);
        }

        public bool TryReadLastAudioFrame(Span<float> destination, out int channels, out int sampleCount)
        {
            return uuavPlayer.TryReadLastAudioFrame(destination, out channels, out sampleCount);
        }
    }
}
