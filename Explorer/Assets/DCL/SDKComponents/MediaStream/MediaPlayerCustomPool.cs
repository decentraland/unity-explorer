using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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
                mediaPlayer.PlatformOptionsWindows._audioMode = Windows.AudioOutput.Unity;
                mediaPlayer.PlatformOptions_macOS.audioMode = MediaPlayer.PlatformOptions.AudioMode.Unity;
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
                float now = UnityEngine.Time.realtimeSinceStartup;
                keysToRemove.Clear();

                foreach (KeyValuePair<string, Queue<MediaPlayerInfo>> kvp in offlineMediaPlayers)
                {
                    Queue<MediaPlayerInfo>? queue = kvp.Value;

                    //IF the video hasnt been used in five minutes, then it will be get closed and destroyed
                    while (queue.Count > 0 && now - queue.Peek().lastTimeUsed > maxOfflineTimePossibleInSeconds)
                    {
                        MediaPlayerInfo? expiredPlayerInfo = queue.Dequeue();
                        expiredPlayerInfo.mediaPlayer.CloseMedia();
                        GameObject.Destroy(expiredPlayerInfo.mediaPlayer.gameObject);
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

            LogMediaPlayerStatus(url, mediaPlayer);

            var control = mediaPlayer.Control;
            //This fix prevents a rare case of crash on MacOS where the close media was called when the media was still being
            //loaded, this caused a crash When CloseMedia() is called while AVPro is still downloading the HLS playlist
            //forcing the Swift continuation for the AVFoundation to never complete
            if (control == null || !control.HasMetaData()) return;

            mediaPlayer.Stop();
            mediaPlayer.CloseMedia();
            mediaPlayer.AudioVolume = 0f;
            mediaPlayer.enabled = false;
            mediaPlayer.gameObject.SetActive(false);

            var info = new MediaPlayerInfo(mediaPlayer, UnityEngine.Time.realtimeSinceStartup);

            if (!offlineMediaPlayers.TryGetValue(url, out Queue<MediaPlayerInfo>? queue))
            {
                queue = new Queue<MediaPlayerInfo>();
                offlineMediaPlayers[url] = queue;
            }

            queue.Enqueue(info);
        }

        private void LogMediaPlayerStatus(string url, MediaPlayer mediaPlayer)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[MediaPlayerPool] ReleaseMediaPlayer diagnostics:");
                sb.AppendLine($"  URL: {url}");

                // Basic object state
                sb.AppendLine($"  MediaPlayer null: {mediaPlayer == null}");
                if (mediaPlayer == null)
                {
                    Debug.LogWarning(sb.ToString());
                    return;
                }

                sb.AppendLine($"  GameObject null: {mediaPlayer.gameObject == null}");
                sb.AppendLine($"  GameObject active: {mediaPlayer.gameObject?.activeSelf}");
                sb.AppendLine($"  Component enabled: {mediaPlayer.enabled}");

                // Media state
                sb.AppendLine($"  MediaOpened: {mediaPlayer.MediaOpened}");
                sb.AppendLine($"  AutoOpen: {mediaPlayer.AutoOpen}");
                sb.AppendLine($"  AudioVolume: {mediaPlayer.AudioVolume}");

                // Platform options (this is what crashed!)
                try
                {
                    #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                    sb.AppendLine($"  PlatformOptions_macOS null: {mediaPlayer.PlatformOptions_macOS == null}");
                    if (mediaPlayer.PlatformOptions_macOS != null)
                    {
                        sb.AppendLine($"  PlatformOptions_macOS.audioMode: {mediaPlayer.PlatformOptions_macOS.audioMode}");
                    }
                    #endif

                    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                    sb.AppendLine($"  PlatformOptionsWindows null: {mediaPlayer.PlatformOptionsWindows == null}");
                    #endif
                }
                catch (Exception e)
                {
                    sb.AppendLine($"  PlatformOptions access ERROR: {e.Message}");
                }

                // Control interface
                var control = mediaPlayer.Control;
                sb.AppendLine($"  Control null: {control == null}");

                if (control != null)
                {
                    try
                    {
                        sb.AppendLine($"  Control.HasMetaData: {control.HasMetaData()}");
                        sb.AppendLine($"  Control.CanPlay: {control.CanPlay()}");
                        sb.AppendLine($"  Control.IsPlaying: {control.IsPlaying()}");
                        sb.AppendLine($"  Control.IsPaused: {control.IsPaused()}");
                        sb.AppendLine($"  Control.IsFinished: {control.IsFinished()}");
                        sb.AppendLine($"  Control.IsSeeking: {control.IsSeeking()}");
                        sb.AppendLine($"  Control.IsBuffering: {control.IsBuffering()}");
                        sb.AppendLine($"  Control.IsLooping: {control.IsLooping()}");
                        sb.AppendLine($"  Control.GetLastError: {control.GetLastError()}");
                        sb.AppendLine($"  Control.GetCurrentTime: {control.GetCurrentTime()}");
                        sb.AppendLine($"  Control.GetPlaybackRate: {control.GetPlaybackRate()}");

                        var bufferedTimes = control.GetBufferedTimes();
                        sb.AppendLine($"  Control.GetBufferedTimes count: {bufferedTimes?.Count ?? -1}");
                    }
                    catch (Exception e)
                    {
                        sb.AppendLine($"  Control access ERROR: {e.Message}");
                    }
                }

                // Info interface
                var info = mediaPlayer.Info;
                sb.AppendLine($"  Info null: {info == null}");

                if (info != null)
                {
                    try
                    {
                        sb.AppendLine($"  Info.GetDuration: {info.GetDuration()}");
                        sb.AppendLine($"  Info.HasVideo: {info.HasVideo()}");
                        sb.AppendLine($"  Info.HasAudio: {info.HasAudio()}");
                        sb.AppendLine($"  Info.GetVideoWidth: {info.GetVideoWidth()}");
                        sb.AppendLine($"  Info.GetVideoHeight: {info.GetVideoHeight()}");
                        sb.AppendLine($"  Info.GetVideoFrameRate: {info.GetVideoFrameRate()}");
                        sb.AppendLine($"  Info.IsPlaybackStalled: {info.IsPlaybackStalled()}");
                    }
                    catch (Exception e)
                    {
                        sb.AppendLine($"  Info access ERROR: {e.Message}");
                    }
                }

                // TextureProducer
                var texProducer = mediaPlayer.TextureProducer;
                sb.AppendLine($"  TextureProducer null: {texProducer == null}");

                if (texProducer != null)
                {
                    try
                    {
                        sb.AppendLine($"  TextureProducer.GetTexture null: {texProducer.GetTexture() == null}");
                        sb.AppendLine($"  TextureProducer.GetTextureFrameCount: {texProducer.GetTextureFrameCount()}");
                    }
                    catch (Exception e)
                    {
                        sb.AppendLine($"  TextureProducer access ERROR: {e.Message}");
                    }
                }

                // AudioSource
                var audioSource = mediaPlayer.AudioSource;
                sb.AppendLine($"  AudioSource null: {audioSource == null}");

                if (audioSource != null)
                {
                    try
                    {
                        sb.AppendLine($"  AudioSource.isPlaying: {audioSource.isPlaying}");
                        sb.AppendLine($"  AudioSource.volume: {audioSource.volume}");
                        sb.AppendLine($"  AudioSource.spatialBlend: {audioSource.spatialBlend}");
                    }
                    catch (Exception e)
                    {
                        sb.AppendLine($"  AudioSource access ERROR: {e.Message}");
                    }
                }

                // Events
                try
                {
                    sb.AppendLine($"  Events null: {mediaPlayer.Events == null}");
                    sb.AppendLine($"  Events.HasListeners: {mediaPlayer.Events?.HasListeners()}");
                }
                catch (Exception e)
                {
                    sb.AppendLine($"  Events access ERROR: {e.Message}");
                }

                // Media path info
                try
                {
                    sb.AppendLine($"  MediaPath.Path: {mediaPlayer.MediaPath?.Path ?? "null"}");
                    sb.AppendLine($"  MediaPath.PathType: {mediaPlayer.MediaPath?.PathType}");
                }
                catch (Exception e)
                {
                    sb.AppendLine($"  MediaPath access ERROR: {e.Message}");
                }

                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[MediaPlayerPool] Error logging diagnostics: {e.Message}\n{e.StackTrace}");
            }
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
