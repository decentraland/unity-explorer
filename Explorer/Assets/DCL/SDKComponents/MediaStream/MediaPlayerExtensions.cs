using DCL.ECSComponents;
using UnityEngine;
using UUAV.Compat;

namespace DCL.SDKComponents.MediaStream
{
    public static class MediaPlayerExtensions
    {
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

        internal static void SetPlaybackProperties(IMediaControl control, PBVideoPlayer sdkVideoPlayer, bool isLiveStream = false) =>
            SetPlaybackProperties(control,
                sdkVideoPlayer.HasPosition ? sdkVideoPlayer.Position : MediaPlayerComponent.DEFAULT_POSITION,
                sdkVideoPlayer is { HasLoop: true, Loop: true },
                sdkVideoPlayer.HasPlaybackRate ? sdkVideoPlayer.PlaybackRate : MediaPlayerComponent.DEFAULT_PLAYBACK_RATE,
                sdkVideoPlayer is { HasPlaying: true, Playing: true },
                isLiveStream);

        internal static void SetPlaybackProperties(IMediaControl control, float position, bool loop, float rate, bool isPlaying, bool isLiveStream = false)
        {
            control.SetLooping(loop);
            control.SetPlaybackRate(rate);

            // For live streams, seeking to a position would move to the beginning of the DVR window.
            // Skip the seek entirely to let the player start at the live edge.
            if (!isLiveStream)
                control.Seek(position);

            if (isPlaying)
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
