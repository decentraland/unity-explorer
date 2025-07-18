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
        }
    }

    [REnum]
    [REnumField(typeof(AvProPlayer))]
    [REnumField(typeof(LivekitPlayer))]
    public partial struct MultiMediaPlayer
    {
        public bool IsPlaying => Match(
            static avPro => avPro.AvProMediaPlayer.Control.IsPlaying(),
            static livekitPlayer => livekitPlayer.State is PlayerState.PLAYING
        );

        public float CurrentTime => Match(
            static avProPlayer => (float)avProPlayer.AvProMediaPlayer.Control.GetCurrentTime(),
            static _ => 0f
        );

        public float Duration => Match(
            static avProPlayer => (float)avProPlayer.AvProMediaPlayer.Info.GetDuration(),
            static _ => 0f
        );

        public bool MediaOpened => Match(
            static avPro => avPro.AvProMediaPlayer.MediaOpened,
            static livekitPlayer => livekitPlayer.MediaOpened
        );

        public bool IsFinished => Match(
            static avPro => avPro.AvProMediaPlayer.Control.IsFinished(),
            static livekitPlayer => livekitPlayer.State is PlayerState.STOPPED
        );

        public bool IsPaused => Match(
            static avPro => avPro.AvProMediaPlayer.Control.IsPaused(),
            static livekitPlayer => livekitPlayer.State is PlayerState.PAUSED
        );

        public bool IsSeeking => Match(
            static avPro => avPro.AvProMediaPlayer.Control.IsSeeking(),
            static _ => false
        );

        public bool HasControl => Match(
            static avPro => avPro.AvProMediaPlayer.Control != null,
            static _ => false
        );

        public void Dispose(MediaAddress address)
        {
            Match(
                address,
                onAvProPlayer: static (address, avPro) =>
                {
                    if (address.IsUrlMediaAddress(out var url))
                        avPro.MediaPlayerCustomPool.ReleaseMediaPlayer(url!.Value.Url, avPro.AvProMediaPlayer);
                },
                onLivekitPlayer: static (_, livekitPlayer) => livekitPlayer.Dispose()
            );
        }

        public void CloseCurrentStream()
        {
            Match(
                static avPro => avPro.AvProMediaPlayer.CloseCurrentStream(),
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
                static (pose, avPlayer) => avPlayer.AvProMediaPlayer.transform.position = pose,
                static (pose, livekitPlayer) => livekitPlayer.PlaceAudioAt(pose)
            );
        }

        public Texture? LastTexture()
        {
            return Match(
                static avPro => avPro.AvProMediaPlayer.TextureProducer.GetTexture(),
                static livekitPlayer => livekitPlayer.LastTexture()
            );
        }

        public void UpdateVolume(float volume)
        {
            Match(
                volume,
                static (ctx, avPro) => avPro.AvProMediaPlayer.AudioVolume = ctx,
                static (ctx, livekitPlayer) => livekitPlayer!.SetVolume(ctx));
        }

        public void CrossfadeVolume(float volume, float volumeDelta = 1)
        {
            Match(
                (volume, volumeDelta),
                static (ctx, avPro) => avPro.AvProMediaPlayer.CrossfadeVolume(ctx.volume, ctx.volumeDelta),
                static (ctx, livekitPlayer) => livekitPlayer!.CrossfadeVolume(ctx.volume, ctx.volumeDelta));
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
                static (ctx, avPro) => avPro.AvProMediaPlayer.UpdatePlayback(ctx.hasPlaying, ctx.isPlaying),
                static (ctx, livekitPlayer) => livekitPlayer.UpdatePlayback(ctx.hasPlaying, ctx.isPlaying)
            );
        }

        public void SetPlaybackProperties(PBVideoPlayer sdkVideoPlayer)
        {
            if (IsAvProPlayer(out var mediaPlayer))
            {
                var avProPlayer = mediaPlayer!.Value.AvProMediaPlayer;
                if (!avProPlayer.MediaOpened) return;
                MediaPlayerExtensions.SetPlaybackPropertiesAsync(avProPlayer.Control!, sdkVideoPlayer).Forget();
            }

            // Livekit streaming doesn't need to adjust playback properties
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
                    bool result = ctx.player.IsLivekitPlayer(out var livekitPlayer);
                    livekitPlayer?.OpenMedia(address);
                    return result;
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
                static avPro => avPro.AvProMediaPlayer.Control.Play(),
                static livekitPlayer => livekitPlayer.Play()
            );
        }

        public void Pause()
        {
            Match(
                static avPro => avPro.AvProMediaPlayer.Control.Pause(),
                static livekitPlayer => livekitPlayer.Pause()
            );
        }

        public ErrorCode GetLastError()
        {
            return Match(
                static avPro => avPro.AvProMediaPlayer.Control.GetLastError(),
                static _ => ErrorCode.None
            );
        }

        public void EnsurePlaying()
        {
            if (IsLivekitPlayer(out var livekitPlayer))
                livekitPlayer!.EnsurePlaying();

            // AvPro doesn't require ensure
        }
    }
}
