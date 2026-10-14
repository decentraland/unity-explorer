using UnityEngine;

namespace UUAV.Compat
{
    // AVPro-compatible facade over UUAVPlayer. The consumer is poll-based
    // (no event subscriptions), see the compat plan for the full mapping.
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

        public bool OpenMedia(MediaPathType pathType, string path, bool autoPlay)
        {
            // Only AbsolutePathOrURL is used; UUAV takes the URL/path verbatim.
            uuavPlayer.OpenMedia(path);

            if (autoPlay)
            {
                uuavPlayer.Play();
            }

            return true;
        }

        // Forwards to Control.Stop (pause + seek to the start), mirroring
        // AVPro's component-level Stop.
        public void Stop()
        {
            backend.Stop();
        }

        public void CloseMedia()
        {
            uuavPlayer.CloseMedia();
        }
    }
}
