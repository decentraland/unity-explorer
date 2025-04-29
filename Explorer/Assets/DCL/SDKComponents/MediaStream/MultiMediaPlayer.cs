using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using RenderHeads.Media.AVProVideo;
using System;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    public enum PlayerState
    {
        PAUSED,
        PLAYING,
        STOPPED,
    }

    public readonly struct MultiMediaPlayer
    {
        public static readonly MultiMediaPlayer EMPTY = new (MediaAddress.Kind.URL, null, null, null);

        private readonly MediaAddress.Kind mediaKind;

        private readonly MediaPlayer? avProMediaPlayer;
        private readonly MediaPlayerCustomPool? mediaPlayerCustomPool;

        private readonly LivekitPlayer? livekitMediaPlayer;

        public bool IsPlaying => mediaKind switch
                                 {
                                     MediaAddress.Kind.URL => avProMediaPlayer!.Control!.IsPlaying(),
                                     MediaAddress.Kind.LIVEKIT => livekitMediaPlayer!.State is PlayerState.PLAYING,
                                     _ => throw new ArgumentOutOfRangeException()
                                 };

        public float CurrentTime => mediaKind switch
                                    {
                                        MediaAddress.Kind.URL => (float)avProMediaPlayer!.Control!.GetCurrentTime(),
                                        MediaAddress.Kind.LIVEKIT => 0,
                                        _ => throw new ArgumentOutOfRangeException()
                                    };

        public float Duration => mediaKind switch
                                 {
                                     MediaAddress.Kind.URL => (float)avProMediaPlayer!.Info!.GetDuration(),
                                     MediaAddress.Kind.LIVEKIT => 0,
                                     _ => throw new ArgumentOutOfRangeException()
                                 };

        public bool MediaOpened => mediaKind switch
                                   {
                                       MediaAddress.Kind.URL => avProMediaPlayer!.MediaOpened,
                                       MediaAddress.Kind.LIVEKIT => livekitMediaPlayer!.MediaOpened,
                                       _ => throw new ArgumentOutOfRangeException()
                                   };
        public bool IsFinished => mediaKind switch
                                  {
                                      MediaAddress.Kind.URL => avProMediaPlayer!.Control!.IsFinished(),
                                      MediaAddress.Kind.LIVEKIT => livekitMediaPlayer!.State is PlayerState.STOPPED,
                                      _ => throw new ArgumentOutOfRangeException()
                                  };
        public bool IsPaused => mediaKind switch
                                {
                                    MediaAddress.Kind.URL => avProMediaPlayer!.Control!.IsPaused(),
                                    MediaAddress.Kind.LIVEKIT => livekitMediaPlayer!.State is PlayerState.PAUSED,
                                    _ => throw new ArgumentOutOfRangeException()
                                };
        public bool IsSeeking => mediaKind switch
                                 {
                                     MediaAddress.Kind.URL => avProMediaPlayer!.Control!.IsSeeking(),
                                     MediaAddress.Kind.LIVEKIT => false,
                                     _ => throw new ArgumentOutOfRangeException()
                                 };

        public bool HasControl => mediaKind switch
                                  {
                                      MediaAddress.Kind.URL => avProMediaPlayer!.Control != null,
                                      MediaAddress.Kind.LIVEKIT => false,
                                      _ => throw new ArgumentOutOfRangeException(),
                                  };

        private MultiMediaPlayer(MediaAddress.Kind mediaKind, MediaPlayer? avProMediaPlayer, MediaPlayerCustomPool? mediaPlayerCustomPool, LivekitPlayer? livekitMediaPlayer)
        {
            this.mediaKind = mediaKind;
            this.avProMediaPlayer = avProMediaPlayer;
            this.mediaPlayerCustomPool = mediaPlayerCustomPool;
            this.livekitMediaPlayer = livekitMediaPlayer;
        }

        public static MultiMediaPlayer NewAvProMediaPlayer(string url, MediaPlayerCustomPool objectPool) =>
            new (MediaAddress.Kind.URL, objectPool.GetOrCreateReusableMediaPlayer(url), objectPool, null);

        public static MultiMediaPlayer NewLiveKitMediaPlayer(LivekitPlayer videoStream) =>
            new (MediaAddress.Kind.LIVEKIT, null, null, videoStream);

        public void Dispose(MediaAddress address)
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.URL:
                    mediaPlayerCustomPool!.ReleaseMediaPlayer(address.Url, avProMediaPlayer!);
                    break;
                case MediaAddress.Kind.LIVEKIT:
                    livekitMediaPlayer!.Dispose();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void CloseCurrentStream()
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.URL:
                    avProMediaPlayer!.CloseCurrentStream();
                    break;
                case MediaAddress.Kind.LIVEKIT:
                    livekitMediaPlayer!.CloseCurrentStream();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Needed for positional sound
        /// </summary>
        public void PlaceAt(Vector3 position)
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.URL:
                    avProMediaPlayer!.transform.position = position;
                    break;
                case MediaAddress.Kind.LIVEKIT:
                    livekitMediaPlayer!.PlaceAudioAt(position);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public Texture? LastTexture() =>
            mediaKind switch
            {
                MediaAddress.Kind.URL => avProMediaPlayer!.TextureProducer!.GetTexture(),
                MediaAddress.Kind.LIVEKIT => livekitMediaPlayer!.LastTexture(),
                _ => throw new ArgumentOutOfRangeException()
            };

        public void UpdateVolume(bool isCurrentScene, bool hasVolume, float volume)
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.URL:
                    avProMediaPlayer!.UpdateVolume(isCurrentScene, hasVolume, volume);
                    break;
                case MediaAddress.Kind.LIVEKIT:
                    float target = hasVolume ? volume : MediaPlayerComponent.DEFAULT_VOLUME;
                    livekitMediaPlayer!.SetVolume(target);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void UpdatePlaybackProperties(PBVideoPlayer sdkVideoPlayer)
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.URL:
                    var mediaPlayer = avProMediaPlayer!;
                    if (!mediaPlayer.MediaOpened) return;
                    mediaPlayer.UpdatePlaybackProperties(sdkVideoPlayer);
                    break;
                case MediaAddress.Kind.LIVEKIT:
                    // Livekit streaming doesn't need to adjust playback properties
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void UpdatePlayback(bool hasPlaying, bool isPlaying)
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.URL:
                    avProMediaPlayer!.UpdatePlayback(hasPlaying, isPlaying);
                    break;
                case MediaAddress.Kind.LIVEKIT:
                    livekitMediaPlayer!.UpdatePlayback(hasPlaying, isPlaying);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void SetPlaybackProperties(PBVideoPlayer sdkVideoPlayer)
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.URL:
                    var mediaPlayer = avProMediaPlayer!;
                    if (!mediaPlayer.MediaOpened) return;
                    MediaPlayerExtensions.SetPlaybackPropertiesAsync(mediaPlayer.Control!, sdkVideoPlayer).Forget();
                    break;
                case MediaAddress.Kind.LIVEKIT:
                    // Livekit streaming doesn't need to adjust playback properties
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void OpenMedia(MediaAddress mediaAddress, bool isFromContentServer, bool autoPlay)
        {
            switch (mediaAddress.MediaKind)
            {
                case MediaAddress.Kind.URL:
                    //The problem is that video files coming from our content server are flagged as application/octet-stream,
                    //but mac OS without a specific content type cannot play them. (more info here https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/2008 )
                    //This adds a query param for video files from content server to force the correct content type

                    //VideoPlayer may be reused
                    if (avProMediaPlayer!.MediaOpened)
                        return;

                    string url = mediaAddress.Url;
                    avProMediaPlayer!.OpenMedia(MediaPathType.AbsolutePathOrURL, isFromContentServer ? string.Format("{0}?includeMimeType", url) : url, autoPlay);
                    break;
                case MediaAddress.Kind.LIVEKIT:
                    var livekitAddress = mediaAddress.Livekit;
                    livekitMediaPlayer!.OpenMedia(livekitAddress);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public bool TryGetAvProPlayer(out MediaPlayer? mediaPlayer)
        {
            if (avProMediaPlayer == null)
            {
                mediaPlayer = null;
                return false;
            }

            mediaPlayer = avProMediaPlayer;
            return true;
        }

        public void TrySeek(double seekTime)
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.URL: avProMediaPlayer!.Control!.Seek(seekTime); break;
                case MediaAddress.Kind.LIVEKIT:
                    // Livekit streaming doesn't support seeking
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void Play()
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.URL:
                    avProMediaPlayer!.Control!.Play();
                    break;
                case MediaAddress.Kind.LIVEKIT:
                    livekitMediaPlayer!.Play();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void Pause()
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.URL:
                    avProMediaPlayer!.Control!.Pause();
                    break;
                case MediaAddress.Kind.LIVEKIT:
                    livekitMediaPlayer!.Pause();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public ErrorCode GetLastError() =>
            mediaKind switch
            {
                MediaAddress.Kind.URL => avProMediaPlayer!.Control!.GetLastError(),
                MediaAddress.Kind.LIVEKIT => ErrorCode.None,
                _ => throw new ArgumentOutOfRangeException()
            };

        public void EnsurePlaying()
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.URL:
                    //ignore
                    break;
                case MediaAddress.Kind.LIVEKIT:
                    livekitMediaPlayer!.EnsurePlaying();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}
