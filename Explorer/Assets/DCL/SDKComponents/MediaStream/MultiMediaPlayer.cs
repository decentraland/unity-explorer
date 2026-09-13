using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.VideoPlayback;
using REnum;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    public enum PlayerState
    {
        Paused,
        Playing,
        Stopped,
    }

    public class UrlMediaPlayer
    {
        public readonly MediaPlayer Player;
        public readonly MediaPlayerCustomPool MediaPlayerCustomPool;

        /// <summary>
        /// This is here because UpdateMediaPlayerSystem uses an async method SetPlaybackProperties that needs to
        /// complete before we do anything else, otherwise it might overwrite a later call to
        /// UpdatePlaybackProperties, if that call is done before it completes. Check issue #6974 for more info.
        /// </summary>
        public bool WaitingForProperties;

        public UrlMediaPlayer(MediaPlayer player, MediaPlayerCustomPool mediaPlayerCustomPool)
        {
            this.Player = player;
            this.MediaPlayerCustomPool = mediaPlayerCustomPool;
        }
    }

    [REnum]
    [REnumField(typeof(UrlMediaPlayer))]
    [REnumField(typeof(LivekitPlayer))]
    public partial struct MultiMediaPlayer
    {
        public bool IsPlaying => Match(
            static urlPlayer => urlPlayer.Player.Control.IsPlaying(),
            static livekitPlayer => livekitPlayer.State is PlayerState.Playing
        );

        public float CurrentTime => Match(
            static urlMediaPlayer => (float)urlMediaPlayer.Player.Control.GetCurrentTime(),
            static _ => 0f
        );

        public float Duration => Match(
            static urlMediaPlayer => (float)urlMediaPlayer.Player.Info.GetDuration(),
            static _ => 0f
        );

        public bool IsFinished => Match(
            static urlPlayer => urlPlayer.Player.Control.IsFinished(),
            static livekitPlayer => livekitPlayer.State is PlayerState.Stopped
        );

        public bool IsPaused => Match(
            static urlPlayer => urlPlayer.Player.Control.IsPaused(),
            static livekitPlayer => livekitPlayer.State is PlayerState.Paused
        );

        public bool IsSeeking => Match(
            static urlPlayer => urlPlayer.Player.Control.IsSeeking(),
            static _ => false
        );

        public bool IsBuffering => Match(
            static urlPlayer => urlPlayer.Player.Control.IsBuffering(),
            static _ => false
        );

        public bool HasControl => Match(
            static urlPlayer => urlPlayer.Player.HasControl,
            static _ => false
        );

        // False when the MediaPlayer GameObject was destroyed (pool eviction / scene teardown). LiveKit is always valid.
        public bool IsValid => Match(
            static urlPlayer => urlPlayer.Player != null,
            static _ => true
        );

        public bool IsReady => Match(
            static urlPlayer => urlPlayer.Player.IsReady,
            static _ => true
        );

        public bool WaitingForProperties => Match(
            static urlPlayer => urlPlayer.WaitingForProperties,
            static _ => false
        );

        public Vector2 GetTexureScale => Match(static urlPlayer =>
            {
                float vScale = urlPlayer.Player.TextureProducer.RequiresVerticalFlip() ? -1 : 1;
                return new Vector2(1, vScale);
            },
            static livekitPlayer => livekitPlayer.CurrentTextureScale
        );

        public bool IsSpatial => Match(static urlPlayer => Mathf.Approximately(urlPlayer.Player.AudioSource.spatialBlend, 1f),
            static _ => false);

        public float SpatialMaxDistance => Match(
            static urlPlayer => urlPlayer.Player.AudioSource.maxDistance,
            static _ => 0f);

        public float SpatialMinDistance => Match(
            static urlPlayer => urlPlayer.Player.AudioSource.minDistance,
            static _ => 0f);

        public void Dispose(MediaAddress address)
        {
            Match(
                address,
                onUrlMediaPlayer: static (address, urlPlayer) =>
                {
                    if (address.IsUrlMediaAddress(out var url))
                        urlPlayer.MediaPlayerCustomPool.ReleaseMediaPlayer(url.Url, urlPlayer.Player);
                },
                onLivekitPlayer: static (_, livekitPlayer) => livekitPlayer.Dispose()
            );
        }

        public void CloseCurrentStream()
        {
            Match(
                static urlPlayer => urlPlayer.Player.CloseCurrentStream(),
                static livekitPlayer => livekitPlayer.CloseCurrentStream()
            );
        }

        /// <summary>
        /// Needed for positional sound
        /// </summary>
        public void PlaceAt(Vector3 position)
        {
            Match(
                position,
                static (pose, urlPlayer) => urlPlayer.Player.transform.position = pose,
                static (pose, livekitPlayer) => livekitPlayer.PlaceAudioAt(pose)
            );
        }

        public Texture? LastTexture()
        {
            return Match(
                static urlPlayer => urlPlayer.Player.TextureProducer.GetTexture(),
                static livekitPlayer => livekitPlayer.LastTexture()
            );
        }

        public void UpdateVolume(float volume)
        {
            Match(
                volume,
                static (ctx, urlPlayer) => urlPlayer.Player.AudioVolume = ctx,
                static (ctx, livekitPlayer) => livekitPlayer!.SetVolume(ctx));
        }

        public void CrossfadeVolume(float volume, float volumeDelta = 1)
        {
            Match(
                (volume, volumeDelta),
                static (ctx, urlPlayer) => urlPlayer.Player.CrossfadeVolume(ctx.volume, ctx.volumeDelta),
                static (ctx, livekitPlayer) => livekitPlayer!.CrossfadeVolume(ctx.volume, ctx.volumeDelta));
        }

        public void UpdatePlaybackProperties(PBVideoPlayer sdkVideoPlayer)
        {
            if (IsUrlMediaPlayer(out var urlMediaPlayer))
            {
                MediaPlayer mediaPlayer = urlMediaPlayer.Player;
                if (!mediaPlayer.MediaOpened) return;
                mediaPlayer.UpdatePlaybackProperties(sdkVideoPlayer);
            }

            // Livekit streaming doesn't need to adjust playback properties
        }

        public void UpdatePlayback(bool hasPlaying, bool isPlaying)
        {
            Match(
                (hasPlaying, isPlaying),
                static (ctx, urlPlayer) => urlPlayer.Player.UpdatePlayback(ctx.hasPlaying, ctx.isPlaying),
                static (ctx, livekitPlayer) => livekitPlayer.UpdatePlayback(ctx.hasPlaying, ctx.isPlaying)
            );
        }

        public void SetLooping(bool isLooping) =>
            Match(
                isLooping,
                static (ctx, urlPlayer) => urlPlayer.Player.Control.SetLooping(ctx),
                static (_, _) => { });

        public async UniTaskVoid SetPlaybackPropertiesAsync(PBVideoPlayer sdkVideoPlayer, bool isLiveStream = false)
        {
            if (IsUrlMediaPlayer(out var mediaPlayer))
            {
                MediaPlayer urlMediaPlayer = mediaPlayer.Player;
                if (!urlMediaPlayer.MediaOpened) return;
                mediaPlayer.WaitingForProperties = true;
                await MediaPlayerExtensions.SetPlaybackPropertiesAsync(urlMediaPlayer.Control, sdkVideoPlayer, isLiveStream);
                mediaPlayer.WaitingForProperties = false;
            }

            // Livekit streaming doesn't need to adjust playback properties
        }

        public void SetPlaybackProperties(CustomMediaStream customMediaStream)
        {
            if (IsUrlMediaPlayer(out var mediaPlayer))
            {
                MediaPlayer urlMediaPlayer = mediaPlayer.Player;
                if (!urlMediaPlayer.MediaOpened) return;
                MediaPlayerExtensions.SetPlaybackPropertiesAsync(urlMediaPlayer.Control, MediaPlayerComponent.DEFAULT_POSITION, customMediaStream.Loop, MediaPlayerComponent.DEFAULT_PLAYBACK_RATE, true).Forget();
            }
        }

        public bool OpenMedia(MediaAddress mediaAddress, bool isFromContentServer, bool autoPlay)
        {
            return mediaAddress.Match(
                (player: this, isFromContentServer, autoPlay),
                onUrlMediaAddress: static (ctx, address) =>
                {
                    //The problem is that video files coming from our content server are flagged as application/octet-stream,
                    //but mac OS without a specific content type cannot play them.
                    //This adds a query param for video files from content server to force the correct content type

                    if (ctx.player.IsUrlMediaPlayer(out var urlMediaPlayer) == false)
                        return false;

                    MediaPlayer player = urlMediaPlayer.Player;

                    //VideoPlayer may be reused
                    if (player.MediaOpened)
                        return true;

                    string target = ctx.isFromContentServer ? string.Format("{0}?includeMimeType", address.Url) : address.Url;
                    return player.OpenMedia(MediaPathType.AbsolutePathOrURL, target, ctx.autoPlay);
                },
                onLivekitAddress: static (ctx, address) =>
                {
                    bool result = ctx.player.IsLivekitPlayer(out var livekitPlayer);
                    livekitPlayer?.OpenMedia(address);
                    return result;
                }
            );
        }

        public bool TryGetUrlMediaPlayer(out MediaPlayer? mediaPlayer)
        {
            if (IsUrlMediaPlayer(out var urlMediaPlayer))
            {
                mediaPlayer = urlMediaPlayer.Player;
                return true;
            }

            mediaPlayer = null;
            return false;
        }

        public void TrySeek(double seekTime)
        {
            if (IsUrlMediaPlayer(out var urlMediaPlayer))
                urlMediaPlayer.Player.Control.Seek(seekTime);

            // Livekit streaming doesn't support seeking
        }

        public void Play()
        {
            Match(
                static urlPlayer => urlPlayer.Player.Control.Play(),
                static livekitPlayer => livekitPlayer.Play()
            );
        }

        public void Pause()
        {
            Match(
                static urlPlayer => urlPlayer.Player.Control.Pause(),
                static livekitPlayer => livekitPlayer.Pause()
            );
        }

        public ErrorCode GetLastError()
        {
            return Match(
                static urlPlayer => urlPlayer.Player.Control.GetLastError(),
                static _ => ErrorCode.None
            );
        }

        public void UpdateSpatialAudio(bool isSpatial, float minDistance, float maxDistance)
        {
            Match((isSpatial, minDistance, maxDistance),
                static (args, urlPlayer) =>
                {
                    AudioSource audioSource = urlPlayer.Player.AudioSource;
                    if (audioSource == null) return;
                    audioSource.spatialBlend = args.isSpatial ? 1f : 0f;
                    audioSource.minDistance = args.minDistance;
                    audioSource.maxDistance = args.maxDistance;
                    audioSource.rolloffMode = AudioRolloffMode.Linear;
                },
                static (_, _) => { });
        }

        /// <summary>
        /// MUST be used in place, caller doesn't take ownership of the reference.
        /// </summary>
        public AudioSource? AnyExposedAudioSource()
        {
            return Match(
                static urlPlayer => urlPlayer.Player.AudioSource,
                static livekitPlayer => livekitPlayer.AnyExposedAudioSource()
            );
        }
    }
}
