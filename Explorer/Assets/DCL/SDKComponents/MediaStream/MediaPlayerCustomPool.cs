using Cysharp.Threading.Tasks;
using RenderHeads.Media.AVProVideo;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.MediaStream
{
    public class MediaPlayerCustomPool
    {
        private readonly MediaPlayer mediaPlayerPrefab;
        private readonly Dictionary<string, Stack<MediaPlayer>> OfflineMediaPlayers;
        private readonly Transform rootContainerTransform;

        public MediaPlayerCustomPool(MediaPlayer mediaPlayerPrefab)
        {
            this.mediaPlayerPrefab = mediaPlayerPrefab;
            rootContainerTransform = new GameObject("POOL_CONTAINER_MEDIA_PLAYER").transform;
            OfflineMediaPlayers = new Dictionary<string, Stack<MediaPlayer>>();
            TryUnloadAsync().Forget();
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
                mediaPlayer = Object.Instantiate(mediaPlayerPrefab, rootContainerTransform);
                //Add other options if we release on other platforms :D
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
                await UniTask.Delay(TimeSpan.FromMinutes(2));

                foreach (KeyValuePair<string, Stack<MediaPlayer>> kvp in OfflineMediaPlayers)
                {
                    foreach (MediaPlayer? player in kvp.Value)
                    {
                        player.CloseMedia();
                        GameObject.Destroy(player.gameObject);
                    }
                }

                OfflineMediaPlayers.Clear();
            }
        }

        public void ReleaseMediaPlayer(string url, MediaPlayer mediaPlayer)
        {
            mediaPlayer.Stop();
            mediaPlayer.enabled = false;
            mediaPlayer.gameObject.SetActive(false);

            mediaPlayer.CloseMedia();
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
