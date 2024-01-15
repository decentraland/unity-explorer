using RenderHeads.Media.AVProVideo;

namespace ECS.Unity.AudioStreams
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
