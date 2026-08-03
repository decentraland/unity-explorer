using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.AvProSwitch;
using System;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    public static class MediaPlayerExtensions
    {
        public static void CloseCurrentStream(this MediaPlayer mediaPlayer)
        {
            mediaPlayer.Stop();
            mediaPlayer.CloseMedia();

            if (mediaPlayer.Events.HasListeners())
                mediaPlayer.Events.RemoveAllListeners();
        }

        public static void CrossfadeVolume(this MediaPlayer mediaPlayer, float targetVolume, float volumeDelta)
        {
            mediaPlayer.AudioVolume = mediaPlayer.AudioVolume > targetVolume
                ? Mathf.Max(0, mediaPlayer.AudioVolume - volumeDelta)
                : Mathf.Min(targetVolume, mediaPlayer.AudioVolume + volumeDelta);
        }

        public static void UpdatePlayback(this MediaPlayer mediaPlayer, bool hasPlaying, bool playing)
        {
            if (!mediaPlayer.MediaOpened)
                return;

            MediaPlayerBackend control = mediaPlayer.Control;

            if (hasPlaying)
            {
                if (playing != control.IsPlaying())
                {
                    if (playing)
                        control.Play();
                    else
                        control.Pause();
                }
            }
            else if (control.IsPlaying())
                control.Stop();
        }

        public static void UpdatePlayback(this LivekitPlayer mediaPlayer, bool hasPlaying, bool playing)
        {
            if (!mediaPlayer.MediaOpened)
                return;

            if (hasPlaying)
            {
                if (playing != mediaPlayer.State is PlayerState.Playing)
                {
                    if (playing)
                        mediaPlayer.Play();
                    else
                        mediaPlayer.Pause();
                }
            }
            else if (mediaPlayer.State is PlayerState.Playing)
                mediaPlayer.Stop();
        }

        internal static UniTask SetPlaybackPropertiesAsync(MediaPlayerBackend control, PBVideoPlayer sdkVideoPlayer, bool isLiveStream = false) =>
            SetPlaybackPropertiesAsync(control,
                sdkVideoPlayer.HasPosition ? sdkVideoPlayer.Position : MediaPlayerComponent.DEFAULT_POSITION,
                sdkVideoPlayer is { HasLoop: true, Loop: true },
                sdkVideoPlayer.HasPlaybackRate ? sdkVideoPlayer.PlaybackRate : MediaPlayerComponent.DEFAULT_PLAYBACK_RATE,
                sdkVideoPlayer is { HasPlaying: true, Playing: true },
                isLiveStream);

        private const float LOAD_WAIT_SECONDS = 1f;
        private const int LOAD_ATTEMPTS = 3;

        internal static async UniTask SetPlaybackPropertiesAsync(MediaPlayerBackend control, float position, bool loop, float rate, bool isPlaying, bool isLiveStream = false)
        {
            // configuring or seeking before the media loads is rejected by UUAV and
            // can make AVPro restart from the start
            var loaded = false;

            for (var attempt = 1; attempt <= LOAD_ATTEMPTS && !loaded; attempt++)
            {
                loaded = await WaitUntilLoadedAsync(control, LOAD_WAIT_SECONDS);

                if (!loaded)
                    ReportHub.LogWarning(ReportCategory.MEDIA_STREAM,
                        $"Media not loaded within {LOAD_WAIT_SECONDS:0.#}s (attempt {attempt}/{LOAD_ATTEMPTS}); retrying");
            }

            if (!loaded)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, "Media failed to load; skipping playback setup");
                return;
            }

            control.SetLooping(loop);
            control.SetPlaybackRate(rate);

            // seeking a live stream moves to the beginning of the DVR window
            if (!isLiveStream)
                control.Seek(position);

            if (isPlaying)
                control.Play();
        }

        // Loaded = seekable (not still Opening) AND holding buffered data, so a seek
        // lands instead of being dropped.
        private static async UniTask<bool> WaitUntilLoadedAsync(MediaPlayerBackend control, float timeoutSeconds)
        {
            // bare `Time` binds to the DCL.Time namespace in this assembly
            float deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSeconds;

            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                if (control.IsReady && control.GetBufferedTimes().Count > 0)
                    return true;

                await UniTask.Yield();
            }

            return control.IsReady && control.GetBufferedTimes().Count > 0;
        }

        public static void UpdatePlaybackProperties(this MediaPlayer mediaPlayer, PBVideoPlayer sdkVideoPlayer)
        {
            if (!mediaPlayer.MediaOpened || !mediaPlayer.IsReady) return;

            MediaPlayerBackend control = mediaPlayer.Control;

            if (sdkVideoPlayer.HasLoop && sdkVideoPlayer.Loop != control.IsLooping())
                control.SetLooping(sdkVideoPlayer.Loop);

            if (sdkVideoPlayer.HasPlaybackRate && !Mathf.Approximately(control.GetPlaybackRate(), sdkVideoPlayer.PlaybackRate))
                control.SetPlaybackRate(sdkVideoPlayer.PlaybackRate);

            if (sdkVideoPlayer.HasPosition)
                control.Seek(sdkVideoPlayer.Position);
        }
    }
}
