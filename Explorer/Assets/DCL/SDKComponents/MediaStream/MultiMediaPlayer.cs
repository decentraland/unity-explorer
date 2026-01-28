using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using RenderHeads.Media.AVProVideo;
using REnum;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    public enum PlayerState
    {
        PAUSED,
        PLAYING,
        STOPPED,
    }

    public readonly struct AvProPlayer
    {
        public readonly MediaPlayer AvProMediaPlayer;
        public readonly MediaPlayerCustomPool MediaPlayerCustomPool;

        public AvProPlayer(MediaPlayer avProMediaPlayer, MediaPlayerCustomPool mediaPlayerCustomPool)
        {
            this.AvProMediaPlayer = avProMediaPlayer;
            this.MediaPlayerCustomPool = mediaPlayerCustomPool;

            if (AvProMediaPlayer.TryGetComponent(out AudioSource audioSource))
                AvProMediaPlayer.SetAudioSource(audioSource);
        }
    }

    [REnum]
    [REnumField(typeof(AvProPlayer))]

#if !NO_LIVEKIT_MODE
    [REnumField(typeof(LivekitPlayer))]
#endif

    public partial struct MultiMediaPlayer
    {
        public bool IsPlaying => Match(
            static avPro => avPro.AvProMediaPlayer.Control.IsPlaying()
#if !NO_LIVEKIT_MODE
            , static livekitPlayer => livekitPlayer.State is PlayerState.PLAYING
#endif
        );

        public float CurrentTime => Match(
            static avProPlayer => (float)avProPlayer.AvProMediaPlayer.Control.GetCurrentTime()
#if !NO_LIVEKIT_MODE
            , static _ => 0f
#endif
        );

        public float Duration => Match(
            static avProPlayer => (float)avProPlayer.AvProMediaPlayer.Info.GetDuration()
#if !NO_LIVEKIT_MODE
            , static _ => 0f
#endif
        );

        public bool IsFinished => Match(
            static avPro => avPro.AvProMediaPlayer.Control.IsFinished()
#if !NO_LIVEKIT_MODE
            , static livekitPlayer => livekitPlayer.State is PlayerState.STOPPED
#endif
        );

        public bool IsPaused => Match(
            static avPro => avPro.AvProMediaPlayer.Control.IsPaused()
#if !NO_LIVEKIT_MODE
            , static livekitPlayer => livekitPlayer.State is PlayerState.PAUSED
#endif

        );

        public bool IsSeeking => Match(
            static avPro => avPro.AvProMediaPlayer.Control.IsSeeking()
#if !NO_LIVEKIT_MODE
            , static _ => false
#endif

        );

        public bool IsBuffering => Match(
            static avPro => avPro.AvProMediaPlayer.Control.IsBuffering()
#if !NO_LIVEKIT_MODE
            , static _ => false
#endif
        );

        public bool HasControl => Match(
            static avPro => avPro.AvProMediaPlayer.Control != null
#if !NO_LIVEKIT_MODE
            , static _ => false
#endif
        );

        public bool IsReady => Match(
            static avPro => avPro.AvProMediaPlayer.TextureProducer != null
#if !NO_LIVEKIT_MODE
            , static _ => true
#endif
        );

        public Vector2 GetTexureScale => Match(static avPro =>
            {
                float vScale = avPro.AvProMediaPlayer.TextureProducer.RequiresVerticalFlip() ? -1 : 1;
                return new Vector2(1, vScale);
            }
#if !NO_LIVEKIT_MODE
            , static _ => new Vector2(1, -1)
#endif
        );

        public bool IsSpatial => Match(
                static avPro => Mathf.Approximately(avPro.AvProMediaPlayer.AudioSource?.spatialBlend ?? 0f, 1f)
#if !NO_LIVEKIT_MODE
                , static _ => false
#endif
                );

        public float SpatialMaxDistance => Match(
            static avPro => avPro.AvProMediaPlayer.AudioSource?.maxDistance 
            ?? MediaPlayerComponent.DEFAULT_SPATIAL_MAX_DISTANCE
#if !NO_LIVEKIT_MODE
            , static _ => 0f
#endif
            );

        public float SpatialMinDistance => Match(
            static avPro => avPro.AvProMediaPlayer.AudioSource?.minDistance ?? MediaPlayerComponent.DEFAULT_SPATIAL_MIN_DISTANCE
#if !NO_LIVEKIT_MODE
            , static _ => 0f
#endif
            );

        public void Dispose(MediaAddress address)
        {
            Match(
                address,
                onAvProPlayer: static (address, avPro) =>
                {
                    if (address.IsUrlMediaAddress(out var url))
                        avPro.MediaPlayerCustomPool.ReleaseMediaPlayer(url!.Value.Url, avPro.AvProMediaPlayer);
                }
#if !NO_LIVEKIT_MODE
                , onLivekitPlayer: static (_, livekitPlayer) => livekitPlayer.Dispose()
#endif
            );
        }

        public void CloseCurrentStream()
        {
            Match(
                static avPro => avPro.AvProMediaPlayer.CloseCurrentStream()
#if !NO_LIVEKIT_MODE
                , static livekitPlayer => livekitPlayer.CloseCurrentStream()
#endif
            );
        }

        /// <summary>
        /// Needed for positional sound
        /// </summary>
        public void PlaceAt(Vector3 position)
        {
            Match(
                position,
                static (pose, avPlayer) => avPlayer.AvProMediaPlayer.transform.position = pose
#if !NO_LIVEKIT_MODE
                , static (pose, livekitPlayer) => livekitPlayer.PlaceAudioAt(pose)
#endif
            );
        }

        public Texture? LastTexture()
        {
            return Match(
                static avPro => avPro.AvProMediaPlayer.TextureProducer.GetTexture()
#if !NO_LIVEKIT_MODE
                , static livekitPlayer => livekitPlayer.LastTexture()
#endif
            );
        }

        public void UpdateVolume(float volume)
        {
            Match(
                volume,
                static (ctx, avPro) => avPro.AvProMediaPlayer.AudioVolume = ctx
#if !NO_LIVEKIT_MODE
                , static (ctx, livekitPlayer) => livekitPlayer!.SetVolume(ctx)
#endif
            );
        }

        public readonly void CrossfadeVolume(float volume, float volumeDelta = 1)
        {
            Match(
                (volume, volumeDelta),
                static (ctx, avPro) => avPro.AvProMediaPlayer.CrossfadeVolume(ctx.volume, ctx.volumeDelta)
#if !NO_LIVEKIT_MODE
                , static (ctx, livekitPlayer) => livekitPlayer!.CrossfadeVolume(ctx.volume, ctx.volumeDelta)
#endif
                );
        }

        public void UpdatePlaybackProperties(PBVideoPlayer sdkVideoPlayer)
        {
            if (IsAvProPlayer(out var avProPlayer))
            {
                var mediaPlayer = avProPlayer!.Value.AvProMediaPlayer;
                if (!mediaPlayer.MediaOpened) return;
                mediaPlayer.UpdatePlaybackProperties(sdkVideoPlayer);
            }

            // Livekit streaming doesn't need to adjust playback properties
        }

        public void UpdatePlayback(bool hasPlaying, bool isPlaying)
        {
            Match(
                (hasPlaying, isPlaying),
                static (ctx, avPro) => avPro.AvProMediaPlayer.UpdatePlayback(ctx.hasPlaying, ctx.isPlaying)
#if !NO_LIVEKIT_MODE
                , static (ctx, livekitPlayer) => livekitPlayer.UpdatePlayback(ctx.hasPlaying, ctx.isPlaying)
#endif
            );
        }

        public readonly void SetLooping(bool isLooping) =>
            Match(
                isLooping,
                static (ctx, avPro) => avPro.AvProMediaPlayer.Control.SetLooping(ctx)
#if !NO_LIVEKIT_MODE
                , static (_, _) => { }
#endif
                );

        public readonly void SetPlaybackProperties(PBVideoPlayer sdkVideoPlayer)
        {
            if (IsAvProPlayer(out var mediaPlayer))
            {
                var avProPlayer = mediaPlayer!.Value.AvProMediaPlayer;
                if (!avProPlayer.MediaOpened) return;
                MediaPlayerExtensions.SetPlaybackPropertiesAsync(avProPlayer.Control!, sdkVideoPlayer).Forget();
            }

            // Livekit streaming doesn't need to adjust playback properties
        }

        public readonly void SetPlaybackProperties(CustomMediaStream customMediaStream)
        {
            if (IsAvProPlayer(out AvProPlayer? mediaPlayer))
            {
                MediaPlayer avProPlayer = mediaPlayer!.Value.AvProMediaPlayer;
                if (!avProPlayer.MediaOpened) return;
                MediaPlayerExtensions.SetPlaybackPropertiesAsync(avProPlayer.Control!, MediaPlayerComponent.DEFAULT_POSITION, customMediaStream.Loop, MediaPlayerComponent.DEFAULT_PLAYBACK_RATE, true).Forget();
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

                    if (ctx.player.IsAvProPlayer(out var avProPlayer) == false)
                        return false;

                    var player = avProPlayer!.Value.AvProMediaPlayer;

                    //VideoPlayer may be reused
                    if (player.MediaOpened)
                        return true;

                    string target = ctx.isFromContentServer ? string.Format("{0}?includeMimeType", address.Url) : address.Url;
                    return player.OpenMedia(MediaPathType.AbsolutePathOrURL, target, ctx.autoPlay);
                },
                onLivekitAddress: static (ctx, address) =>
                {
#if !NO_LIVEKIT_MODE
                    bool result = ctx.player.IsLivekitPlayer(out var livekitPlayer);
                    livekitPlayer?.OpenMedia(address);
                    return result;
#else
                    return false;
#endif
                }
            );
        }

        public bool TryGetAvProPlayer(out MediaPlayer? mediaPlayer)
        {
            bool result = IsAvProPlayer(out var avProPlayer);
            mediaPlayer = avProPlayer?.AvProMediaPlayer;
            return result;
        }

        public void TrySeek(double seekTime)
        {
            if (IsAvProPlayer(out var avProPlayer))
                avProPlayer!.Value.AvProMediaPlayer.Control.Seek(seekTime);

            // Livekit streaming doesn't support seeking
        }

        public void Play()
        {
            Match(
                static avPro => avPro.AvProMediaPlayer.Control.Play()
#if !NO_LIVEKIT_MODE
                , static livekitPlayer => livekitPlayer.Play()
#endif
            );
        }

        public void Pause()
        {
            Match(
                static avPro => avPro.AvProMediaPlayer.Control.Pause()
#if !NO_LIVEKIT_MODE
                , 
                static livekitPlayer => livekitPlayer.Pause()
#endif
            );
        }

        public ErrorCode GetLastError()
        {
            return Match(
                static avPro => avPro.AvProMediaPlayer.Control.GetLastError()
#if !NO_LIVEKIT_MODE
                , 
                static _ => ErrorCode.None
#endif
            );
        }

        public void UpdateSpatialAudio(bool isSpatial, float minDistance, float maxDistance)
        {
            Match((isSpatial, minDistance, maxDistance),
                static (args, avPro) =>
                {
                    AudioSource audioSource = avPro.AvProMediaPlayer.AudioSource;
                    if (audioSource == null) return;
                    audioSource.spatialBlend = args.isSpatial ? 1f : 0f;
                    audioSource.minDistance = args.minDistance;
                    audioSource.maxDistance = args.maxDistance;
                    audioSource.rolloffMode = AudioRolloffMode.Linear;
                }
#if !NO_LIVEKIT_MODE
                , static (_, _) => { }
#endif
                );
        }

        /// <summary>
        /// MUST be used in place, caller doesn't take ownership of the referene.
        /// Caveat: AVProVideo uses direct audio output bypassing Unity's audio system.
        /// It such cases the exposure is not possible through this method.
        /// </summary>
        public AudioSource? ExposedAudioSource()
        {
            return Match(
                static avPro => avPro.AvProMediaPlayer.AudioSource
#if !NO_LIVEKIT_MODE
                , static livekitPlayer => livekitPlayer.ExposedAudioSource()
#endif
            );
        }
    }
}
