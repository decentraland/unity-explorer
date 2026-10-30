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
    }
}
