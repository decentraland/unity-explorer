using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.WebRequests;
using RenderHeads.Media.AVProVideo;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    public static class MediaPlayerExtensions
    {
        public static async UniTask OpenMediaIfReachableAsync(this MediaPlayer mediaPlayer, IWebRequestController webRequestController, string url, bool autoPlay, CancellationToken ct, Action onComplete = null)
        {
            if (await webRequestController.IsReachableAsync(URLAddress.FromString(url), ct))
            {
                mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, url, autoPlay);
                onComplete?.Invoke();
            }
            else
                mediaPlayer.CloseCurrentStream();
        }

        public static void SetPlaybackProperties(this MediaPlayer mediaPlayer, PBVideoPlayer sdkVideoPlayer)
        {
            if (!mediaPlayer.MediaOpened) return;

            IMediaControl control = mediaPlayer.Control;

            control.SetLooping(sdkVideoPlayer.HasLoop && sdkVideoPlayer.Loop); // default: false
            control.SetPlaybackRate(sdkVideoPlayer.HasPlaybackRate ? sdkVideoPlayer.PlaybackRate : MediaPlayerComponent.DEFAULT_PLAYBACK_RATE);
            control.SeekFast(sdkVideoPlayer.HasPosition ? sdkVideoPlayer.Position : MediaPlayerComponent.DEFAULT_POSITION);
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

        public static void UpdatePlaybackProperties(this MediaPlayer mediaPlayer, PBVideoPlayer sdkVideoPlayer)
        {
            if (!mediaPlayer.MediaOpened) return;

            IMediaControl control = mediaPlayer.Control;

            if (sdkVideoPlayer.HasLoop && sdkVideoPlayer.Loop != control.IsLooping())
                control.SetLooping(sdkVideoPlayer.Loop);

            if (sdkVideoPlayer.HasPlaybackRate && !Mathf.Approximately(control.GetPlaybackRate(), sdkVideoPlayer.PlaybackRate))
                control.SetPlaybackRate(sdkVideoPlayer.PlaybackRate);

            if (sdkVideoPlayer.HasPosition)
                control.SeekFast(sdkVideoPlayer.Position);
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

        public static void CloseCurrentStream(this MediaPlayer mediaPlayer)
        {
            mediaPlayer.Stop();
            mediaPlayer.CloseMedia();

            if (mediaPlayer.Events.HasListeners())
                mediaPlayer.Events.RemoveAllListeners();
        }
    }
}
