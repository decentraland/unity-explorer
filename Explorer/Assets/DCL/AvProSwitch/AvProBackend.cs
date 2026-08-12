using UnityEngine;
using AVPro = RenderHeads.Media.AVProVideo;

namespace DCL.AvProSwitch
{
    // AVPro path: enables the real AVPro MediaPlayer + AudioOutput components
    // serialized (disabled) on the prefab and forwards to them. AVPro creates
    // its platform player lazily (Start or first OpenMedia), so the inner
    // Control/Info/TextureProducer are null until then and every forward
    // guards with an inert default.
    public sealed class AvProBackend
    {
        private static readonly TimeRanges NOT_BUFFERED = new TimeRanges(0);
        private static readonly TimeRanges BUFFERED = new TimeRanges(1);

        private readonly AVPro.MediaPlayer player;
        private readonly MediaPlayer.OptionsWindows optionsWindows;
        private readonly MediaPlayer.OptionsApple optionsApple;

        public AvProBackend(GameObject gameObject, MediaPlayer.OptionsWindows optionsWindows, MediaPlayer.OptionsApple optionsApple)
        {
            this.optionsWindows = optionsWindows;
            this.optionsApple = optionsApple;

            if (!gameObject.TryGetComponent(out player))
                player = gameObject.AddComponent<AVPro.MediaPlayer>();

            if (!gameObject.TryGetComponent(out AVPro.AudioOutput audioOutput))
            {
                audioOutput = gameObject.AddComponent<AVPro.AudioOutput>();
                audioOutput.Player = player;
            }

            player.SetAudioSource(gameObject.GetComponent<AudioSource>());

            // The pooled prefab must never open its serialized MediaPath on its
            // own; enabling the component would honor a stray _autoOpen edit.
            player.AutoOpen = false;
            player.enabled = true;
            audioOutput.enabled = true;
        }

        public AudioSource AudioSource => player.AudioSource;

        public bool MediaOpened => player.MediaOpened;

        public bool HasControl => player.Control != null;

        public bool IsReady => player.TextureProducer != null;

        public float AudioVolume
        {
            get => player.AudioVolume;
            set => player.AudioVolume = value;
        }

        public bool OpenMedia(MediaPathType pathType, string path, bool autoPlay)
        {
            // The pool writes the switch's option objects after Instantiate;
            // AVPro reads its own at platform-player creation, which OpenMedia
            // triggers, so this is the last moment to copy them across.
            ApplyOptions();
            return player.OpenMedia(ToAvPro(pathType), path, autoPlay);
        }

        public void CloseMedia() =>
            player.CloseMedia();

        public void Play() => player.Control?.Play();

        public void Pause() => player.Control?.Pause();

        // AVPro's component-level Stop only forwards to Control.Stop, so a
        // single method covers both call sites.
        public void Stop() => player.Stop();

        public void Seek(double time) => player.Control?.Seek(time);

        public bool IsPlaying() => player.Control?.IsPlaying() ?? false;

        public bool IsPaused() => player.Control?.IsPaused() ?? false;

        public bool IsSeeking() => player.Control?.IsSeeking() ?? false;

        public bool IsBuffering() => player.Control?.IsBuffering() ?? false;

        public bool IsFinished() => player.Control?.IsFinished() ?? false;

        public bool IsLooping() => player.Control?.IsLooping() ?? false;

        public void SetLooping(bool looping) => player.Control?.SetLooping(looping);

        public double GetCurrentTime() => player.Control?.GetCurrentTime() ?? 0.0;

        public float GetPlaybackRate() => player.Control?.GetPlaybackRate() ?? 1f;

        public void SetPlaybackRate(float rate) => player.Control?.SetPlaybackRate(rate);

        // Same members and values on both sides, so the numeric cast is exact.
        public ErrorCode GetLastError() =>
            player.Control != null ? (ErrorCode)player.Control.GetLastError() : ErrorCode.None;

        // The consumer reads only Count as a "buffered range exists" gate, so
        // the two cached instances cover it without per-poll allocations.
        public TimeRanges GetBufferedTimes()
        {
            AVPro.TimeRanges? ranges = player.Control?.GetBufferedTimes();
            return ranges is { Count: > 0 } ? BUFFERED : NOT_BUFFERED;
        }

        public double GetDuration() => player.Info?.GetDuration() ?? 0.0;

        public Texture? GetTexture(int index = 0) => player.TextureProducer?.GetTexture(index);

        public bool RequiresVerticalFlip() => player.TextureProducer?.RequiresVerticalFlip() ?? false;

        private void ApplyOptions()
        {
            AVPro.MediaPlayer.OptionsWindows target = player.PlatformOptionsWindows;
            target._audioMode = ToAvPro(optionsWindows._audioMode);
            target.videoApi = ToAvPro(optionsWindows.videoApi);
            target.startWithHighestBitrate = optionsWindows.startWithHighestBitrate;
            target.useLowLiveLatency = optionsWindows.useLowLiveLatency;

            player.PlatformOptions_macOS.audioMode = ToAvPro(optionsApple.audioMode);
        }

        private static AVPro.MediaPathType ToAvPro(MediaPathType pathType) =>
            pathType switch
            {
                MediaPathType.AbsolutePathOrURL => AVPro.MediaPathType.AbsolutePathOrURL,
                MediaPathType.RelativeToProjectFolder => AVPro.MediaPathType.RelativeToProjectFolder,
                MediaPathType.RelativeToStreamingAssetsFolder => AVPro.MediaPathType.RelativeToStreamingAssetsFolder,
                MediaPathType.RelativeToDataFolder => AVPro.MediaPathType.RelativeToDataFolder,
                MediaPathType.RelativeToPersistentDataFolder => AVPro.MediaPathType.RelativeToPersistentDataFolder,
                _ => AVPro.MediaPathType.AbsolutePathOrURL,
            };

        private static AVPro.Windows.AudioOutput ToAvPro(Windows.AudioOutput audioOutput) =>
            audioOutput switch
            {
                Windows.AudioOutput.System => AVPro.Windows.AudioOutput.System,
                Windows.AudioOutput.Unity => AVPro.Windows.AudioOutput.Unity,
                Windows.AudioOutput.FacebookAudio360 => AVPro.Windows.AudioOutput.FacebookAudio360,
                Windows.AudioOutput.None => AVPro.Windows.AudioOutput.None,
                _ => AVPro.Windows.AudioOutput.Unity,
            };

        private static AVPro.Windows.VideoApi ToAvPro(Windows.VideoApi videoApi) =>
            videoApi switch
            {
                Windows.VideoApi.MediaFoundation => AVPro.Windows.VideoApi.MediaFoundation,
                Windows.VideoApi.DirectShow => AVPro.Windows.VideoApi.DirectShow,
                Windows.VideoApi.WinRT => AVPro.Windows.VideoApi.WinRT,
                _ => AVPro.Windows.VideoApi.MediaFoundation,
            };

        private static AVPro.MediaPlayer.PlatformOptions.AudioMode ToAvPro(MediaPlayer.PlatformOptions.AudioMode audioMode) =>
            audioMode switch
            {
                MediaPlayer.PlatformOptions.AudioMode.SystemDirect => AVPro.MediaPlayer.PlatformOptions.AudioMode.SystemDirect,
                MediaPlayer.PlatformOptions.AudioMode.Unity => AVPro.MediaPlayer.PlatformOptions.AudioMode.Unity,
                MediaPlayer.PlatformOptions.AudioMode.FacebookAudio360 => AVPro.MediaPlayer.PlatformOptions.AudioMode.FacebookAudio360,

                // AVPro has no "None"; the consumer never sets it, and Unity
                // routing is the project-wide audio mode.
                MediaPlayer.PlatformOptions.AudioMode.None => AVPro.MediaPlayer.PlatformOptions.AudioMode.Unity,
                _ => AVPro.MediaPlayer.PlatformOptions.AudioMode.Unity,
            };
    }
}
