using DCL.ECSComponents;
using RenderHeads.Media.AVProVideo;

namespace DCL.SDKComponents.MediaStream
{
    public static class MediaPlayerExtensions
    {
        public static MediaPlayer UpdateVolume(this MediaPlayer mediaPlayer, bool isCurrentScene, bool hasVolume, float volume)
        {
            if (isCurrentScene)
                mediaPlayer.AudioVolume = hasVolume ? volume : MediaPlayerComponent.DEFAULT_VOLUME;
            else
                mediaPlayer.AudioVolume = 0f;

            return mediaPlayer;
        }

        public static MediaPlayer UpdatePlayback(this MediaPlayer mediaPlayer, bool hasPlaying, bool playing)
        {
            if (hasPlaying && playing != mediaPlayer.Control.IsPlaying())
            {
                if (playing)
                    mediaPlayer.Play();
                else
                    mediaPlayer.Stop();
            }

            return mediaPlayer;
        }

        public static void UpdatePlaybackProperties(this MediaPlayer mediaPlayer, PBVideoPlayer sdkVideoPlayer)
        {
            mediaPlayer.Loop = sdkVideoPlayer.HasLoop && sdkVideoPlayer.Loop; // default: loop = false
            mediaPlayer.Control.SetPlaybackRate(sdkVideoPlayer.HasPlaybackRate ? sdkVideoPlayer.PlaybackRate : MediaPlayerComponent.DEFAULT_PLAYBACK_RATE);
            mediaPlayer.Control.Seek(sdkVideoPlayer.HasPosition ? sdkVideoPlayer.Position : MediaPlayerComponent.DEFAULT_POSITION);
        }

        public static void CloseCurrentStream(this MediaPlayer mediaPlayer)
        {
            mediaPlayer.Stop();
            mediaPlayer.CloseMedia();
            mediaPlayer.Events.RemoveAllListeners();
        }
    }
}
