using RenderHeads.Media.AVProVideo;

namespace DCL.SDKComponents.AudioStream
{
    public static class MediaPlayerExtensions
    {
        public static void CloseCurrentStream(this MediaPlayer mediaPlayer)
        {
            if (mediaPlayer == null) return;

            mediaPlayer.Stop();
            mediaPlayer.CloseMedia();
            mediaPlayer.Events.RemoveAllListeners();
        }
    }
}
