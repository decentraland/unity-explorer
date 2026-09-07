using UnityEngine;

namespace DCL.VideoPlayback
{
    // The client's media playback surface. Attaches the UUAV facade component
    // at Awake and forwards the poll-based control/info/texture surface the
    // media systems consume.
    [RequireComponent(typeof(AudioSource))]
    public sealed class MediaPlayer : MonoBehaviour
    {
        private UuavBackend backend = null!;

        public UuavBackend Control => backend;
        public UuavBackend Info => backend;
        public UuavBackend TextureProducer => backend;
        public MediaPlayerEvent Events { get; } = new MediaPlayerEvent();

        public AudioSource AudioSource => backend.AudioSource;

        public bool MediaOpened => backend.MediaOpened;

        public bool HasControl => backend.HasControl;

        public bool IsReady => backend.IsReady;

        public float AudioVolume
        {
            get => backend.AudioVolume;
            set => backend.AudioVolume = value;
        }

        private void Awake()
        {
            backend = new UuavBackend(gameObject);
        }

        public bool OpenMedia(MediaPathType pathType, string path, bool autoPlay) =>
            backend.OpenMedia(pathType, path, autoPlay);

        public void Stop() =>
            backend.Stop();

        public void CloseMedia() =>
            backend.CloseMedia();
    }
}
