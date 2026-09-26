using UnityEngine;

namespace DCL.AvProSwitch
{
    // Runtime switch between the two media player backends: the in-house UUAV
    // player (behind the UUAV.Compat facade) and RenderHeads AVPro. The backend
    // is attached once per instance at Awake, driven by
    // MediaPlayerBackendSelection, which the composition root sets from the
    // "use-custom-media-player" feature flag before any player is provisioned.
    [RequireComponent(typeof(AudioSource))]
    public sealed class MediaPlayer : MonoBehaviour
    {
        // Base for the per-platform option objects; holds the shared AudioMode
        // enum the macOS options reference.
        public class PlatformOptions
        {
            public enum AudioMode
            {
                SystemDirect,
                Unity,
                FacebookAudio360,
                None,
            }
        }

        public sealed class OptionsWindows : PlatformOptions
        {
            public Windows.AudioOutput _audioMode = Windows.AudioOutput.Unity;
            public Windows.VideoApi videoApi = Windows.VideoApi.MediaFoundation;
            public bool startWithHighestBitrate;
            public bool useLowLiveLatency;
        }

        public sealed class OptionsApple : PlatformOptions
        {
            public AudioMode audioMode = AudioMode.Unity;
        }

        private MediaPlayerBackend backend;

        public OptionsWindows PlatformOptionsWindows { get; } = new OptionsWindows();
        public OptionsApple PlatformOptions_macOS { get; } = new OptionsApple();

        public MediaPlayerBackend Control => backend;
        public MediaPlayerBackend Info => backend;
        public MediaPlayerBackend TextureProducer => backend;
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
            backend = MediaPlayerBackendSelection.UseCustomPlayer
                ? MediaPlayerBackend.FromUuavBackend(new UuavBackend(gameObject))
                : MediaPlayerBackend.FromAvProBackend(new AvProBackend(gameObject, PlatformOptionsWindows, PlatformOptions_macOS));
        }

        public bool OpenMedia(MediaPathType pathType, string path, bool autoPlay) =>
            backend.OpenMedia(pathType, path, autoPlay);

        public void Stop() =>
            backend.Stop();

        public void CloseMedia() =>
            backend.CloseMedia();
    }
}
