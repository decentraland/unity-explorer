using UnityEngine;
using Compat = UUAV.Compat;

namespace DCL.AvProSwitch
{
    // UUAV path: attaches the UUAV.Compat facade component and forwards to it,
    // converting between the facade's AVPro-shaped subset types and the
    // switch's structurally identical ones. Platform options are inert here,
    // matching the facade (audio always routes through Unity, video always
    // uses the D3D11 path).
    public sealed class UuavBackend
    {
        private static readonly TimeRanges NOT_BUFFERED = new TimeRanges(0);
        private static readonly TimeRanges BUFFERED = new TimeRanges(1);

        private readonly Compat.MediaPlayer player;

        public UuavBackend(GameObject gameObject)
        {
            player = gameObject.AddComponent<Compat.MediaPlayer>();
        }

        public AudioSource AudioSource => player.AudioSource;

        public bool MediaOpened => player.MediaOpened;

        // The compat facade builds its control and texture surfaces in Awake,
        // so they exist for the component's whole lifetime.
        public bool HasControl => true;

        public bool IsReady => true;

        public float AudioVolume
        {
            get => player.AudioVolume;
            set => player.AudioVolume = value;
        }

        public bool OpenMedia(MediaPathType pathType, string path, bool autoPlay) =>
            player.OpenMedia(ToCompat(pathType), path, autoPlay);

        public void CloseMedia() =>
            player.CloseMedia();

        public void Play() => player.Control.Play();

        public void Pause() => player.Control.Pause();

        // Pauses and seeks back to the start, matching AVPro's Stop semantics.
        public void Stop() => player.Control.Stop();

        public void Seek(double time) => player.Control.Seek(time);

        public bool IsPlaying() => player.Control.IsPlaying();

        public bool IsPaused() => player.Control.IsPaused();

        public bool IsSeeking() => player.Control.IsSeeking();

        public bool IsBuffering() => player.Control.IsBuffering();

        public bool IsFinished() => player.Control.IsFinished();

        public bool IsLooping() => player.Control.IsLooping();

        public void SetLooping(bool looping) => player.Control.SetLooping(looping);

        public double GetCurrentTime() => player.Control.GetCurrentTime();

        public float GetPlaybackRate() => player.Control.GetPlaybackRate();

        public void SetPlaybackRate(float rate) => player.Control.SetPlaybackRate(rate);

        // Same members and values on both sides, so the numeric cast is exact.
        public ErrorCode GetLastError() =>
            (ErrorCode)player.Control.GetLastError();

        // The consumer reads only Count as a "buffered range exists" gate, so
        // the two cached instances cover it without per-poll allocations.
        public TimeRanges GetBufferedTimes() =>
            player.Control.GetBufferedTimes().Count > 0 ? BUFFERED : NOT_BUFFERED;

        public double GetDuration() => player.Info.GetDuration();

        public Texture? GetTexture(int index = 0) => player.TextureProducer.GetTexture(index);

        public bool RequiresVerticalFlip() => player.TextureProducer.RequiresVerticalFlip();

        private static Compat.MediaPathType ToCompat(MediaPathType pathType) =>
            pathType switch
            {
                MediaPathType.AbsolutePathOrURL => Compat.MediaPathType.AbsolutePathOrURL,
                MediaPathType.RelativeToProjectFolder => Compat.MediaPathType.RelativeToProjectFolder,
                MediaPathType.RelativeToStreamingAssetsFolder => Compat.MediaPathType.RelativeToStreamingAssetsFolder,
                MediaPathType.RelativeToDataFolder => Compat.MediaPathType.RelativeToDataFolder,
                MediaPathType.RelativeToPersistentDataFolder => Compat.MediaPathType.RelativeToPersistentDataFolder,
                _ => Compat.MediaPathType.AbsolutePathOrURL,
            };
    }
}
