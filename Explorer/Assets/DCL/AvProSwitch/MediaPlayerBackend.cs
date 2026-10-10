using REnum;
using UnityEngine;

namespace DCL.AvProSwitch
{
    // The two media player backends as a tagged union: dispatch is a tag
    // switch via Match, no interfaces involved. Also serves as the polled
    // control/info/texture surface the consumer reaches through
    // MediaPlayer.Control / .Info / .TextureProducer.
    [REnum]
    [REnumField(typeof(UuavBackend))]
    [REnumField(typeof(AvProBackend))]
    public partial struct MediaPlayerBackend
    {
        public AudioSource AudioSource => Match(
            static uuav => uuav.AudioSource,
            static avPro => avPro.AudioSource);

        public bool MediaOpened => Match(
            static uuav => uuav.MediaOpened,
            static avPro => avPro.MediaOpened);

        // AVPro creates its platform player lazily (Start or first OpenMedia);
        // these report whether the underlying control/texture surfaces exist yet.
        public bool HasControl => Match(
            static uuav => uuav.HasControl,
            static avPro => avPro.HasControl);

        public bool IsReady => Match(
            static uuav => uuav.IsReady,
            static avPro => avPro.IsReady);

        public float AudioVolume
        {
            get => Match(
                static uuav => uuav.AudioVolume,
                static avPro => avPro.AudioVolume);

            set => Match(
                value,
                static (volume, uuav) => uuav.AudioVolume = volume,
                static (volume, avPro) => avPro.AudioVolume = volume);
        }

        public bool OpenMedia(MediaPathType pathType, string path, bool autoPlay) =>
            Match(
                (pathType, path, autoPlay),
                static (ctx, uuav) => uuav.OpenMedia(ctx.pathType, ctx.path, ctx.autoPlay),
                static (ctx, avPro) => avPro.OpenMedia(ctx.pathType, ctx.path, ctx.autoPlay));

        public void CloseMedia() =>
            Match(
                static uuav => uuav.CloseMedia(),
                static avPro => avPro.CloseMedia());

        public void Play() =>
            Match(
                static uuav => uuav.Play(),
                static avPro => avPro.Play());

        public void Pause() =>
            Match(
                static uuav => uuav.Pause(),
                static avPro => avPro.Pause());

        public void Stop() =>
            Match(
                static uuav => uuav.Stop(),
                static avPro => avPro.Stop());

        public void Seek(double time) =>
            Match(
                time,
                static (t, uuav) => uuav.Seek(t),
                static (t, avPro) => avPro.Seek(t));

        public bool IsPlaying() =>
            Match(
                static uuav => uuav.IsPlaying(),
                static avPro => avPro.IsPlaying());

        public bool IsPaused() =>
            Match(
                static uuav => uuav.IsPaused(),
                static avPro => avPro.IsPaused());

        public bool IsSeeking() =>
            Match(
                static uuav => uuav.IsSeeking(),
                static avPro => avPro.IsSeeking());

        public bool IsBuffering() =>
            Match(
                static uuav => uuav.IsBuffering(),
                static avPro => avPro.IsBuffering());

        public bool IsFinished() =>
            Match(
                static uuav => uuav.IsFinished(),
                static avPro => avPro.IsFinished());

        public bool IsLooping() =>
            Match(
                static uuav => uuav.IsLooping(),
                static avPro => avPro.IsLooping());

        public void SetLooping(bool looping) =>
            Match(
                looping,
                static (value, uuav) => uuav.SetLooping(value),
                static (value, avPro) => avPro.SetLooping(value));

        public double GetCurrentTime() =>
            Match(
                static uuav => uuav.GetCurrentTime(),
                static avPro => avPro.GetCurrentTime());

        public float GetPlaybackRate() =>
            Match(
                static uuav => uuav.GetPlaybackRate(),
                static avPro => avPro.GetPlaybackRate());

        public void SetPlaybackRate(float rate) =>
            Match(
                rate,
                static (value, uuav) => uuav.SetPlaybackRate(value),
                static (value, avPro) => avPro.SetPlaybackRate(value));

        public ErrorCode GetLastError() =>
            Match(
                static uuav => uuav.GetLastError(),
                static avPro => avPro.GetLastError());

        public TimeRanges GetBufferedTimes() =>
            Match(
                static uuav => uuav.GetBufferedTimes(),
                static avPro => avPro.GetBufferedTimes());

        public double GetDuration() =>
            Match(
                static uuav => uuav.GetDuration(),
                static avPro => avPro.GetDuration());

        public Texture? GetTexture(int index = 0) =>
            Match(
                index,
                static (i, uuav) => uuav.GetTexture(i),
                static (i, avPro) => avPro.GetTexture(i));

        public bool RequiresVerticalFlip() =>
            Match(
                static uuav => uuav.RequiresVerticalFlip(),
                static avPro => avPro.RequiresVerticalFlip());
    }
}
