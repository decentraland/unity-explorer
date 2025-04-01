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
            if (OfflineMediaPlayers.ContainsKey(url))
            {
                MediaPlayer mediaPlayer = OfflineMediaPlayers[url].Pop();
                mediaPlayer.gameObject.SetActive(true);
                return mediaPlayer;
            }

            MediaPlayer newMediaPlayer = mediaPlayerPool.Get();
            return newMediaPlayer;
        }

        public void ReleaseMediaPlayer(string url, MediaPlayer mediaPlayer)
        {
            mediaPlayer.gameObject.SetActive(false);
            mediaPlayer.Stop();

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
