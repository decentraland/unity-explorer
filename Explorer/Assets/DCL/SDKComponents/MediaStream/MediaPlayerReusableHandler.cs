using DCL.Optimization.Pools;
using RenderHeads.Media.AVProVideo;
using System.Collections.Generic;
using System.Linq;

namespace DCL.SDKComponents.MediaStream
{
    public class MediaPlayerReusableHandler
    {
        public GameObjectPool<MediaPlayer> mediaPlayerPool;
        public Dictionary<string, Stack<MediaPlayer>> OfflineMediaPlayers;

        public MediaPlayerReusableHandler(GameObjectPool<MediaPlayer> mediaPlayerPool)
        {
            OfflineMediaPlayers = new Dictionary<string, Stack<MediaPlayer>>();
            this.mediaPlayerPool = mediaPlayerPool;
        }

        public MediaPlayer TryGetReusableMediaPlayer(string url)
        {
            MediaPlayer mediaPlayer = null;
            if (OfflineMediaPlayers.ContainsKey(url))
            {
                mediaPlayer = OfflineMediaPlayers[url].Pop();
                mediaPlayer.enabled = true;
                mediaPlayer.gameObject.SetActive(true);
            }
            else
            {
                mediaPlayer = mediaPlayerPool.Get();

                //Add other options if we release on other platforms :D
                mediaPlayer.PlatformOptionsWindows.audioOutput = Windows.AudioOutput.Unity;
                mediaPlayer.PlatformOptionsMacOSX.audioMode = MediaPlayer.OptionsApple.AudioMode.Unity;
            }

            mediaPlayer.AutoOpen = false;
            mediaPlayer.enabled = true;

            //HACK: this should be handled by the pool itself.
            mediaPlayer.transform.SetParent(mediaPlayerPool.PoolContainerTransform);
            return mediaPlayer;
        }

        public void ReleaseMediaPlayer(string url, MediaPlayer mediaPlayer)
        {
            mediaPlayer.Stop();
            mediaPlayer.enabled = false;
            mediaPlayer.gameObject.SetActive(false);

            if (!OfflineMediaPlayers.ContainsKey(url))
            {
                Stack<MediaPlayer> mediaPlayerStack = new ();
                mediaPlayerStack.Push(mediaPlayer);
                OfflineMediaPlayers.Add(url, mediaPlayerStack);
            }
            else
                OfflineMediaPlayers[url].Push(mediaPlayer);
        }
    }
}
