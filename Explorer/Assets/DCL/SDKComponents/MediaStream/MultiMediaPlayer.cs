using DCL.ECSComponents;
using REnum;
using UnityEngine;
using UUAV.Compat;

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
        public readonly MediaPlayer MediaPlayer;
        public readonly MediaPlayerCustomPool MediaPlayerCustomPool;

        public UrlMediaPlayer(MediaPlayer mediaPlayer, MediaPlayerCustomPool mediaPlayerCustomPool)
        {
            this.MediaPlayer = mediaPlayer;
            this.MediaPlayerCustomPool = mediaPlayerCustomPool;
        }
    }

    [REnum]
    [REnumField(typeof(UrlMediaPlayer))]
    [REnumField(typeof(LivekitPlayer))]
    public partial struct MultiMediaPlayer
    {
        public bool IsPlaying => Match(
            static urlMediaPlayer => urlMediaPlayer.MediaPlayer.Control.IsPlaying(),
            static livekitPlayer => livekitPlayer.State is PlayerState.Playing
        );

        public float CurrentTime => Match(
            static urlMediaPlayer => (float)urlMediaPlayer.MediaPlayer.Control.GetCurrentTime(),
            static _ => 0f
        );

        public float Duration => Match(
            static urlMediaPlayer => (float)urlMediaPlayer.MediaPlayer.Info.GetDuration(),
            static _ => 0f
        );

        public bool IsFinished => Match(
            static urlMediaPlayer => urlMediaPlayer.MediaPlayer.Control.IsFinished(),
            static livekitPlayer => livekitPlayer.State is PlayerState.Stopped
        );

        public bool IsPaused => Match(
            static urlMediaPlayer => urlMediaPlayer.MediaPlayer.Control.IsPaused(),
            static livekitPlayer => livekitPlayer.State is PlayerState.Paused
        );

        public bool IsSeeking => Match(
            static urlMediaPlayer => urlMediaPlayer.MediaPlayer.Control.IsSeeking(),
            static _ => false
        );

        public bool IsBuffering => Match(
            static urlMediaPlayer => urlMediaPlayer.MediaPlayer.Control.IsBuffering(),
            static _ => false
        );

        // False when the MediaPlayer GameObject was destroyed (pool eviction / scene teardown). LiveKit is always valid.
        public bool IsValid => Match(
            static urlMediaPlayer => urlMediaPlayer.MediaPlayer != null,
            static _ => true
        );

        public Vector2 TextureScale => Match(static urlMediaPlayer =>
            {
                float vScale = urlMediaPlayer.MediaPlayer.TextureProducer.RequiresVerticalFlip() ? -1 : 1;
                return new Vector2(1, vScale);
            },
            static livekitPlayer => livekitPlayer.CurrentTextureScale
        );

        public bool IsSpatial => Match(static urlMediaPlayer => Mathf.Approximately(urlMediaPlayer.MediaPlayer.AudioSource.spatialBlend, 1f),
            static _ => false);

        public float SpatialMaxDistance => Match(
            static urlMediaPlayer => urlMediaPlayer.MediaPlayer.AudioSource.maxDistance,
            static _ => 0f);

        public float SpatialMinDistance => Match(
            static urlMediaPlayer => urlMediaPlayer.MediaPlayer.AudioSource.minDistance,
            static _ => 0f);

        public void Dispose(MediaAddress address)
        {
            Match(
                address,
                onUrlMediaPlayer: static (address, urlMediaPlayer) =>
                {
                    if (address.IsUrlMediaAddress(out var url))
                        urlMediaPlayer.MediaPlayerCustomPool.ReleaseMediaPlayer(url.Url, urlMediaPlayer.MediaPlayer);
                },
                onLivekitPlayer: static (_, livekitPlayer) => livekitPlayer.Dispose()
            );
        }

        public void CloseCurrentStream()
        {
            Match(
                static urlMediaPlayer => urlMediaPlayer.MediaPlayer.CloseMedia(),
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
                static (pose, urlMediaPlayer) => urlMediaPlayer.MediaPlayer.transform.position = pose,
                static (pose, livekitPlayer) => livekitPlayer.PlaceAudioAt(pose)
            );
        }

        public Texture? LastTexture()
        {
            return Match(
                static urlMediaPlayer => urlMediaPlayer.MediaPlayer.TextureProducer.GetTexture(),
                static livekitPlayer => livekitPlayer.LastTexture()
            );
        }

        public void UpdateVolume(float volume)
        {
            Match(
                volume,
                static (ctx, urlMediaPlayer) => urlMediaPlayer.MediaPlayer.AudioVolume = ctx,
                static (ctx, livekitPlayer) => livekitPlayer.SetVolume(ctx));
        }

        public void CrossfadeVolume(float volume, float volumeDelta = 1)
        {
            Match(
                (volume, volumeDelta),
                static (ctx, urlMediaPlayer) => urlMediaPlayer.MediaPlayer.CrossfadeVolume(ctx.volume, ctx.volumeDelta),
                static (ctx, livekitPlayer) => livekitPlayer.CrossfadeVolume(ctx.volume, ctx.volumeDelta));
        }

        public void UpdatePlaybackProperties(PBVideoPlayer sdkVideoPlayer)
        {
            if (IsUrlMediaPlayer(out var urlMediaPlayer))
                urlMediaPlayer.MediaPlayer.UpdatePlaybackProperties(sdkVideoPlayer);

            // Livekit streaming doesn't need to adjust playback properties
        }

        public void UpdatePlayback(bool hasPlaying, bool isPlaying)
        {
            Match(
                (hasPlaying, isPlaying),
                static (ctx, urlMediaPlayer) => urlMediaPlayer.MediaPlayer.UpdatePlayback(ctx.hasPlaying, ctx.isPlaying),
                static (ctx, livekitPlayer) => livekitPlayer.UpdatePlayback(ctx.hasPlaying, ctx.isPlaying)
            );
        }

        public void SetLooping(bool isLooping) =>
            Match(
                isLooping,
                static (ctx, urlMediaPlayer) => urlMediaPlayer.MediaPlayer.Control.SetLooping(ctx),
                static (_, _) => { });

        public void SetPlaybackProperties(PBVideoPlayer sdkVideoPlayer, bool isLiveStream = false)
        {
            if (IsUrlMediaPlayer(out var urlMediaPlayer))
            {
                MediaPlayer mediaPlayer = urlMediaPlayer.MediaPlayer;
                if (!mediaPlayer.MediaOpened) return;
                MediaPlayerExtensions.SetPlaybackProperties(mediaPlayer.Control, sdkVideoPlayer, isLiveStream);
            }

            // Livekit streaming doesn't need to adjust playback properties
        }

        public void SetPlaybackProperties(CustomMediaStream customMediaStream)
        {
            if (IsUrlMediaPlayer(out var urlMediaPlayer))
            {
                MediaPlayer mediaPlayer = urlMediaPlayer.MediaPlayer;
                if (!mediaPlayer.MediaOpened) return;
                MediaPlayerExtensions.SetPlaybackProperties(mediaPlayer.Control, MediaPlayerComponent.DEFAULT_POSITION, customMediaStream.Loop, MediaPlayerComponent.DEFAULT_PLAYBACK_RATE, true);
            }
        }

        public bool OpenMedia(MediaAddress mediaAddress, bool isFromContentServer, bool autoPlay)
        {
            return mediaAddress.Match(
                (player: this, isFromContentServer, autoPlay),
                onUrlMediaAddress: static (ctx, address) =>
                {
                    if (ctx.player.IsUrlMediaPlayer(out var urlMediaPlayer) == false)
                        return false;

                    MediaPlayer player = urlMediaPlayer.MediaPlayer;

                    //VideoPlayer may be reused
                    if (player.MediaOpened)
                        return true;

                    // Inherited from AVPro (RenderHeads #2008: AVFoundation can't open application/octet-stream media,
                    // which is how the content server flags video files). FFmpeg probes the bytes, so re-check
                    // whether UUAV still needs it with a content-server MP4 on macOS and Windows.
                    string target = ctx.isFromContentServer ? string.Format("{0}?includeMimeType", address.Url) : address.Url;
                    return player.OpenMedia(target, ctx.autoPlay);
                },
                onLivekitAddress: static (ctx, address) =>
                {
                    bool result = ctx.player.IsLivekitPlayer(out var livekitPlayer);
                    livekitPlayer?.OpenMedia(address);
                    return result;
                }
            );
        }

        public void TrySeek(double seekTime)
        {
            if (IsUrlMediaPlayer(out var urlMediaPlayer))
                urlMediaPlayer.MediaPlayer.Control.Seek(seekTime);

            // Livekit streaming doesn't support seeking
        }

        public void Play()
        {
            Match(
                static urlMediaPlayer => urlMediaPlayer.MediaPlayer.Control.Play(),
                static livekitPlayer => livekitPlayer.Play()
            );
        }

        public void Pause()
        {
            Match(
                static urlMediaPlayer => urlMediaPlayer.MediaPlayer.Control.Pause(),
                static livekitPlayer => livekitPlayer.Pause()
            );
        }

        public ErrorCode GetLastError()
        {
            return Match(
                static urlMediaPlayer => urlMediaPlayer.MediaPlayer.Control.GetLastError(),
                static _ => ErrorCode.None
            );
        }

        public void UpdateSpatialAudio(bool isSpatial, float minDistance, float maxDistance)
        {
            Match((isSpatial, minDistance, maxDistance),
                static (args, urlMediaPlayer) =>
                {
                    AudioSource audioSource = urlMediaPlayer.MediaPlayer.AudioSource;
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
                static urlMediaPlayer => urlMediaPlayer.MediaPlayer.AudioSource,
                static livekitPlayer => livekitPlayer.AnyExposedAudioSource()
            );
        }
    }
}
