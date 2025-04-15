namespace DCL.SDKComponents.MediaStream
{
    public class MediaPlayerUtils
    {
        public static void CleanUpMediaPlayer(ref MediaPlayerComponent mediaPlayerComponent, MediaPlayerCustomPool mediaPlayerPool)
        {
            mediaPlayerPool.ReleaseMediaPlayer(mediaPlayerComponent.URL, mediaPlayerComponent.MediaPlayer);
            mediaPlayerComponent.Dispose();
        }
    }
}
