using Cysharp.Threading.Tasks;
using RenderHeads.Media.AVProVideo;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.MediaStream
{
    public class MediaPlayerCustomPool
    {
        private readonly MediaPlayer mediaPlayerPrefab;
        private readonly Dictionary<string, Queue<MediaPlayerInfo>> offlineMediaPlayers;
        private readonly Transform rootContainerTransform;

        private readonly List<string> keysToRemove = new ();

        private readonly int tryCleanOfflineMediaPlayersDelayInMinutes = 2;
        private readonly float maxOfflineTimePossibleInSeconds = 300f;

        public MediaPlayerCustomPool(MediaPlayer mediaPlayerPrefab)
        {
            this.mediaPlayerPrefab = mediaPlayerPrefab;
            rootContainerTransform = new GameObject("POOL_CONTAINER_MEDIA_PLAYER").transform;
            offlineMediaPlayers = new Dictionary<string, Queue<MediaPlayerInfo>>();
            TryUnloadAsync().Forget();
        }

        public MediaPlayer GetOrCreateReusableMediaPlayer(string url)
        {
            MediaPlayer mediaPlayer;

            if (offlineMediaPlayers.TryGetValue(url, out Queue<MediaPlayerInfo>? queue) && queue.Count > 0)
            {
                mediaPlayer = queue.Dequeue().mediaPlayer;
                mediaPlayer.enabled = true;
                mediaPlayer.gameObject.SetActive(true);
            }
            else
            {
                mediaPlayer = Object.Instantiate(mediaPlayerPrefab, rootContainerTransform);
                mediaPlayer.PlatformOptionsWindows.audioOutput = Windows.AudioOutput.Unity;
                mediaPlayer.PlatformOptionsMacOSX.audioMode = MediaPlayer.OptionsApple.AudioMode.Unity;
            }

            mediaPlayer.AutoOpen = false;
            mediaPlayer.enabled = true;

            return mediaPlayer;
        }

        private async UniTask TryUnloadAsync()
        {
            while (true)
            {
                //We will do this analysis every two minute
                await UniTask.Delay(TimeSpan.FromMinutes(tryCleanOfflineMediaPlayersDelayInMinutes));
                float now = Time.realtimeSinceStartup;
                keysToRemove.Clear();

                foreach (KeyValuePair<string, Queue<MediaPlayerInfo>> kvp in offlineMediaPlayers)
                {
                    Queue<MediaPlayerInfo>? queue = kvp.Value;

                    //IF the video hasnt been used in five minutes, then it will be get closed and destroyed
                    while (queue.Count > 0 && now - queue.Peek().lastTimeUsed > maxOfflineTimePossibleInSeconds)
                    {
                        MediaPlayerInfo? expiredPlayerInfo = queue.Dequeue();
                        expiredPlayerInfo.mediaPlayer.CloseMedia();
                        UnityObjectUtils.SafeDestroy(expiredPlayerInfo.mediaPlayer.gameObject);
                    }

                    if (queue.Count == 0)
                        keysToRemove.Add(kvp.Key);
                }

                foreach (string key in keysToRemove)
                    offlineMediaPlayers.Remove(key);
            }
        }

        public void ReleaseMediaPlayer(string url, MediaPlayer mediaPlayer)
        {
            //On quit, Unity may have already detroyed the MediaPlayer; so we might get a null-ref
            if (UnityObjectUtils.IsQuitting) return;

            mediaPlayer.Stop();
            mediaPlayer.enabled = false;
            mediaPlayer.gameObject.SetActive(false);

            var info = new MediaPlayerInfo(mediaPlayer, Time.realtimeSinceStartup);

            if (!offlineMediaPlayers.TryGetValue(url, out Queue<MediaPlayerInfo>? queue))
            {
                queue = new Queue<MediaPlayerInfo>();
                offlineMediaPlayers[url] = queue;
            }

            queue.Enqueue(info);
        }
    }

    public class MediaPlayerInfo
    {
        public MediaPlayer mediaPlayer;
        public float lastTimeUsed;

        public MediaPlayerInfo(MediaPlayer mediaPlayer, float lastTimeUsed)
        {
            this.mediaPlayer = mediaPlayer;
            this.lastTimeUsed = lastTimeUsed;
        }
    }
}
