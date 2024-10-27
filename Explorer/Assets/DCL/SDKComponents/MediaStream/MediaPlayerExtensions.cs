using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using RenderHeads.Media.AVProVideo;
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

        public static void UpdateVolume(this MediaPlayer mediaPlayer, bool isCurrentScene, bool hasVolume, float volume)
        {
            if (!isCurrentScene)
                mediaPlayer.AudioVolume = 0f;
            else
            {
                if (!hasVolume)
                    mediaPlayer.AudioVolume = MediaPlayerComponent.DEFAULT_VOLUME;
                else if (!Mathf.Approximately(mediaPlayer.AudioVolume, volume))
                    mediaPlayer.AudioVolume = volume;
            }
        }

        public static MediaPlayer UpdatePlayback(this MediaPlayer mediaPlayer, bool hasPlaying, bool playing)
        {
            if (!mediaPlayer.MediaOpened) return mediaPlayer;

            IMediaControl control = mediaPlayer.Control;

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

            return mediaPlayer;
        }

        public static void SetPlaybackProperties(this MediaPlayer mediaPlayer, PBVideoPlayer sdkVideoPlayer)
        {
            if (!mediaPlayer.MediaOpened) return;
            SetPlaybackPropertiesAsync(mediaPlayer.Control, sdkVideoPlayer).Forget();
        }

        private static async UniTask SetPlaybackPropertiesAsync(IMediaControl control, PBVideoPlayer sdkVideoPlayer)
        {
            // If there are no seekable/buffered times, and we try to seek, AVPro may mistakenly play it from the start.
            await UniTask.WaitUntil(() => control.GetBufferedTimes().Count > 0);

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX)
            // The only way found to make the video initialization consistent and reliable on MacOS even after a scene reload
            await UniTask.Delay(TimeSpan.FromSeconds(1f));
#endif

            control.SetLooping(sdkVideoPlayer is { HasLoop: true, Loop: true }); // default: false
            control.SetPlaybackRate(sdkVideoPlayer.HasPlaybackRate ? sdkVideoPlayer.PlaybackRate : MediaPlayerComponent.DEFAULT_PLAYBACK_RATE);
            control.Seek(sdkVideoPlayer.HasPosition ? sdkVideoPlayer.Position : MediaPlayerComponent.DEFAULT_POSITION);

            if (sdkVideoPlayer is { HasPlaying: true, Playing: true })
                control.Play();
        }

        public static void UpdatePlaybackProperties(this MediaPlayer mediaPlayer, PBVideoPlayer sdkVideoPlayer)
        {
            if (!mediaPlayer.MediaOpened) return;

            IMediaControl control = mediaPlayer.Control;

            if (sdkVideoPlayer.HasLoop && sdkVideoPlayer.Loop != control.IsLooping())
                control.SetLooping(sdkVideoPlayer.Loop);

            if (sdkVideoPlayer.HasPlaybackRate && !Mathf.Approximately(control.GetPlaybackRate(), sdkVideoPlayer.PlaybackRate))
                control.SetPlaybackRate(sdkVideoPlayer.PlaybackRate);

            if (sdkVideoPlayer.HasPosition)
                control.Seek(sdkVideoPlayer.Position);
        }
    }
}
