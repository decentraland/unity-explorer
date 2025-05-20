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
        public static readonly MultiMediaPlayer EMPTY = new (MediaAddress.Kind.UrlMediaAddress, null, null, null);

        private readonly MediaAddress.Kind mediaKind;

        private readonly MediaPlayer? avProMediaPlayer;
        private readonly MediaPlayerCustomPool? mediaPlayerCustomPool;

        private readonly LivekitPlayer? livekitMediaPlayer;

        public bool IsPlaying => mediaKind switch
                                 {
                                     MediaAddress.Kind.UrlMediaAddress => avProMediaPlayer!.Control!.IsPlaying(),
                                     MediaAddress.Kind.LivekitAddress => livekitMediaPlayer!.State is PlayerState.PLAYING,
                                     _ => throw new ArgumentOutOfRangeException()
                                 };

        public float CurrentTime => mediaKind switch
                                    {
                                        MediaAddress.Kind.UrlMediaAddress => (float)avProMediaPlayer!.Control!.GetCurrentTime(),
                                        MediaAddress.Kind.LivekitAddress => 0,
                                        _ => throw new ArgumentOutOfRangeException()
                                    };

        public float Duration => mediaKind switch
                                 {
                                     MediaAddress.Kind.UrlMediaAddress => (float)avProMediaPlayer!.Info!.GetDuration(),
                                     MediaAddress.Kind.LivekitAddress => 0,
                                     _ => throw new ArgumentOutOfRangeException()
                                 };

        public bool MediaOpened => mediaKind switch
                                   {
                                       MediaAddress.Kind.UrlMediaAddress => avProMediaPlayer!.MediaOpened,
                                       MediaAddress.Kind.LivekitAddress => livekitMediaPlayer!.MediaOpened,
                                       _ => throw new ArgumentOutOfRangeException()
                                   };
        public bool IsFinished => mediaKind switch
                                  {
                                      MediaAddress.Kind.UrlMediaAddress => avProMediaPlayer!.Control!.IsFinished(),
                                      MediaAddress.Kind.LivekitAddress => livekitMediaPlayer!.State is PlayerState.STOPPED,
                                      _ => throw new ArgumentOutOfRangeException()
                                  };
        public bool IsPaused => mediaKind switch
                                {
                                    MediaAddress.Kind.UrlMediaAddress => avProMediaPlayer!.Control!.IsPaused(),
                                    MediaAddress.Kind.LivekitAddress => livekitMediaPlayer!.State is PlayerState.PAUSED,
                                    _ => throw new ArgumentOutOfRangeException()
                                };
        public bool IsSeeking => mediaKind switch
                                 {
                                     MediaAddress.Kind.UrlMediaAddress => avProMediaPlayer!.Control!.IsSeeking(),
                                     MediaAddress.Kind.LivekitAddress => false,
                                     _ => throw new ArgumentOutOfRangeException()
                                 };

        public bool HasControl => mediaKind switch
                                  {
                                      MediaAddress.Kind.UrlMediaAddress => avProMediaPlayer!.Control != null,
                                      MediaAddress.Kind.LivekitAddress => false,
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
            new (MediaAddress.Kind.UrlMediaAddress, objectPool.GetOrCreateReusableMediaPlayer(url), objectPool, null);

        public static MultiMediaPlayer NewLiveKitMediaPlayer(LivekitPlayer videoStream) =>
            new (MediaAddress.Kind.LivekitAddress, null, null, videoStream);

        public void Dispose(MediaAddress address)
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.UrlMediaAddress:
                    if (address.IsUrlMediaAddress(out var url))
                        mediaPlayerCustomPool!.ReleaseMediaPlayer(url!.Value.Url, avProMediaPlayer!);

                    break;
                case MediaAddress.Kind.LivekitAddress:
                    livekitMediaPlayer!.Dispose();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void CloseCurrentStream()
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.UrlMediaAddress:
                    avProMediaPlayer!.CloseCurrentStream();
                    break;
                case MediaAddress.Kind.LivekitAddress:
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
                case MediaAddress.Kind.UrlMediaAddress:
                    avProMediaPlayer!.transform.position = position;
                    break;
                case MediaAddress.Kind.LivekitAddress:
                    livekitMediaPlayer!.PlaceAudioAt(position);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public Texture? LastTexture() =>
            mediaKind switch
            {
                MediaAddress.Kind.UrlMediaAddress => avProMediaPlayer!.TextureProducer!.GetTexture(),
                MediaAddress.Kind.LivekitAddress => livekitMediaPlayer!.LastTexture(),
                _ => throw new ArgumentOutOfRangeException()
            };

        public void UpdateVolume(bool isCurrentScene, bool hasVolume, float volume)
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.UrlMediaAddress:
                    avProMediaPlayer!.UpdateVolume(isCurrentScene, hasVolume, volume);
                    break;
                case MediaAddress.Kind.LivekitAddress:
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
                case MediaAddress.Kind.UrlMediaAddress:
                    var mediaPlayer = avProMediaPlayer!;
                    if (!mediaPlayer.MediaOpened) return;
                    mediaPlayer.UpdatePlaybackProperties(sdkVideoPlayer);
                    break;
                case MediaAddress.Kind.LivekitAddress:
                    // Livekit streaming doesn't need to adjust playback properties
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void UpdatePlayback(bool hasPlaying, bool isPlaying)
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.UrlMediaAddress:
                    avProMediaPlayer!.UpdatePlayback(hasPlaying, isPlaying);
                    break;
                case MediaAddress.Kind.LivekitAddress:
                    livekitMediaPlayer!.UpdatePlayback(hasPlaying, isPlaying);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void SetPlaybackProperties(PBVideoPlayer sdkVideoPlayer)
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.UrlMediaAddress:
                    var mediaPlayer = avProMediaPlayer!;
                    if (!mediaPlayer.MediaOpened) return;
                    MediaPlayerExtensions.SetPlaybackPropertiesAsync(mediaPlayer.Control!, sdkVideoPlayer).Forget();
                    break;
                case MediaAddress.Kind.LivekitAddress:
                    // Livekit streaming doesn't need to adjust playback properties
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public bool OpenMedia(MediaAddress mediaAddress, bool isFromContentServer, bool autoPlay)
        {
            return mediaAddress.Match(
                (player: this, isFromContentServer, autoPlay),
                onUrlMediaAddress: static (ctx, address) =>
                {
                    //The problem is that video files coming from our content server are flagged as application/octet-stream,
                    //but mac OS without a specific content type cannot play them. (more info here https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/2008 )
                    //This adds a query param for video files from content server to force the correct content type

                    if (ctx.player.avProMediaPlayer == null)
                        return false;

                    //VideoPlayer may be reused
                    if (ctx.player.avProMediaPlayer.MediaOpened)
                        return true;

                    string target = ctx.isFromContentServer ? string.Format("{0}?includeMimeType", address.Url) : address.Url;
                    return ctx.player.avProMediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, target, ctx.autoPlay);
                },
                onLivekitAddress: static (ctx, address) =>
                {
                    if (ctx.player.livekitMediaPlayer == null) return false;
                    ctx.player.livekitMediaPlayer!.OpenMedia(address);
                    return true;
                }
            );
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
                case MediaAddress.Kind.UrlMediaAddress:
                    avProMediaPlayer!.Control!.Seek(seekTime);
                    break;
                case MediaAddress.Kind.LivekitAddress:
                    // Livekit streaming doesn't support seeking
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void Play()
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.UrlMediaAddress:
                    avProMediaPlayer!.Control!.Play();
                    break;
                case MediaAddress.Kind.LivekitAddress:
                    livekitMediaPlayer!.Play();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void Pause()
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.UrlMediaAddress:
                    avProMediaPlayer!.Control!.Pause();
                    break;
                case MediaAddress.Kind.LivekitAddress:
                    livekitMediaPlayer!.Pause();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public ErrorCode GetLastError() =>
            mediaKind switch
            {
                MediaAddress.Kind.UrlMediaAddress => avProMediaPlayer!.Control!.GetLastError(),
                MediaAddress.Kind.LivekitAddress => ErrorCode.None,
                _ => throw new ArgumentOutOfRangeException()
            };

        public void EnsurePlaying()
        {
            switch (mediaKind)
            {
                case MediaAddress.Kind.UrlMediaAddress:
                    //ignore
                    break;
                case MediaAddress.Kind.LivekitAddress:
                    livekitMediaPlayer!.EnsurePlaying();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}
