using DCL.ECSComponents;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using Utility;
using Plugins.NativeAudioAnalysis;
using UUAV.Compat;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    /// Tracks generic-URL retry state across MediaPlayerComponent re-creation. Lives on the entity
    /// so backoff doesn't reset when UpdateMediaPlayerSystem destroys+recreates the player.
    /// YouTube has its own re-resolve path (ResolvedUrlExpiresAt); this covers everything else.
    /// </summary>
    public struct MediaPlayerRetryState
    {
        public int Attempts;
        public float NextRetryAt;
    }

    public struct MediaPlayerComponent : IComponentWithAudioFrameBuffer
    {
        public const float DEFAULT_VOLUME = 1f;
        public const float DEFAULT_PLAYBACK_RATE = 1f;
        public const float DEFAULT_POSITION = 0f;
        private const float PLAY_CHECK_THRESHOLD = 0.5f;
        public const float DEFAULT_SPATIAL_MIN_DISTANCE = 0f;
        public const float DEFAULT_SPATIAL_MAX_DISTANCE = 60f;

        private float lastVideoTime;
        private float frozenTimestamp;
        private float lastPlayTimestamp;
        private bool isFrozen;

        public MultiMediaPlayer MediaPlayer;
        public readonly bool IsFromContentServer;

        public MediaAddress MediaAddress;
        public VideoState State { get; private set; }
        public bool HasFailed { get; private set; }
        public VideoState LastPropagatedVideoState;
        public float LastPropagatedVideoTime;
        public float ResolvedUrlExpiresAt;
        public bool IsLiveStream;

        /// <summary>
        ///     Tracks the last reported media state for audio events to avoid sending duplicate CRDT messages
        /// </summary>
        public MediaState LastReportedMediaState;
        public CancellationTokenSource? Cts;
        public OpenMediaPromise? OpenMediaPromise;

        public bool IsSpatial => MediaPlayer.IsSpatial;
        public float SpatialMaxDistance => MediaPlayer.SpatialMaxDistance;
        public float SpatialMinDistance => MediaPlayer.SpatialMinDistance;

        /// <summary>
        ///     URL players' DSP output, received through <see cref="UUAV.Compat.MediaPlayer.AudioFrameTap" />.
        /// </summary>
        private LastAudioFrameBuffer? dspAudioFrameBuffer;

        /// <summary>
        ///     Filter-chain fallback for players that only expose an AudioSource (LiveKit).
        ///     Use ThreadSafeLastAudioFrameReadFilter because it has to be attached to the same GameObject.
        ///     But GameObject is owned by AudioSource MonoBehavour in practice, and gets repooled with it.
        ///     To avoid LifeCycle complications ThreadSafeLastAudioFrameReadFilter is referenced directly and owned by AudioSourceComponent.
        ///     MonoBehaviour cannot be easily pooled because the ownership issue arise. 
        ///     AudioSource and ThreadSafeLastAudioFrameReadFilter share the same GameObject.
        /// </summary>
        private ThreadSafeLastAudioFrameReadFilterWrap lastAudioFrameReadFilter;


        public MediaPlayerComponent(MultiMediaPlayer mediaPlayer, bool isFromContentServer) : this()
        {
            MediaPlayer = mediaPlayer;
            IsFromContentServer = isFromContentServer;
            HasFailed = false;
            State = VideoState.VsNone;
            LastReportedMediaState = MediaState.MsNone;
            isFrozen = false;
            lastAudioFrameReadFilter = new ();
        }

        public readonly bool IsPlaying => MediaPlayer.IsPlaying;
        public readonly float CurrentTime => MediaPlayer.CurrentTime;
        public readonly float Duration => MediaPlayer.Duration;

        public VideoState UpdateState()
        {
            var player = MediaPlayer;
            var state = VideoState.VsNone;
            var isNowFrozen = false;

            if (MediaAddress.IsEmpty)
                state = VideoState.VsNone;
            else if (player.IsFinished)
                state = VideoState.VsNone;
            else if (HasFailed || player.GetLastError() != ErrorCode.None)
                state = VideoState.VsError;
            else if (player.IsPaused)
                state = VideoState.VsPaused;
            else if (player.IsPlaying)
            {
                state = VideoState.VsPlaying;

                float timestamp = UnityEngine.Time.realtimeSinceStartup;

                // This threshold solves the case on which it is updated many times in a row in the same frame
                if (timestamp - lastPlayTimestamp > PLAY_CHECK_THRESHOLD)
                {
                    lastPlayTimestamp = timestamp;

                    bool wasFrozen = isFrozen;
                    isNowFrozen = Mathf.Abs(player.CurrentTime - lastVideoTime) < Mathf.Epsilon;

                    if (!isNowFrozen)
                    {
                        lastVideoTime = player.CurrentTime;
                        frozenTimestamp = timestamp;
                    }
                    else
                    {
                        if (isNowFrozen != wasFrozen)
                            frozenTimestamp = timestamp;

                        if (player.IsSeeking)
                            state = VideoState.VsSeeking;
                        else if (player.IsBuffering)
                            state = VideoState.VsBuffering;
                    }
                }
            }

            State = state;
            isFrozen = isNowFrozen;

            return State;
        }

        /// <summary>
        /// Frozen means that the state is playing but the player keeps the same play time.
        /// Most likely because it is seeking or buffering
        /// </summary>
        /// <param name="frozenElapsedTime">Amount of seconds that elapsed since the media player was detected as "frozen"</param>
        /// <returns></returns>
        public readonly bool IsFrozen(out float frozenElapsedTime)
        {
            if (isFrozen)
                frozenElapsedTime = UnityEngine.Time.realtimeSinceStartup - frozenTimestamp;
            else
                frozenElapsedTime = 0f;

            return isFrozen;
        }

        public void MarkAsFailed(bool failed) =>
            HasFailed = failed;

        public bool TryGetOrAttachAudioFrameBuffer([NotNullWhen(true)] out LastAudioFrameBuffer? output)
        {
            if (MediaPlayer.IsUrlMediaPlayer(out UrlMediaPlayer? urlMediaPlayer))
            {
                LastAudioFrameBuffer? buffer = dspAudioFrameBuffer;

                if (buffer == null)
                {
                    buffer = new LastAudioFrameBuffer { SampleRate = AudioSettings.outputSampleRate };
                    urlMediaPlayer.MediaPlayer.AudioFrameTap = buffer.Write;
                    dspAudioFrameBuffer = buffer;
                }

                output = buffer;
                return true;
            }

            AudioSource? audioSource = MediaPlayer.AnyExposedAudioSource();

            if (audioSource == null)
            {
                output = null;
                return false;
            }

            return lastAudioFrameReadFilter.TryAttachLastAudioFrameReadFilterOrUseExisting(audioSource, out output);
        }

        public void EnsureAudioFrameBufferIsRemoved()
        {
            lastAudioFrameReadFilter.EnsureLastAudioFrameReadFilterIsRemoved();

            if (dspAudioFrameBuffer == null) return;

            dspAudioFrameBuffer = null;

            // The pooled player outlives this component: detach the tap so the next owner starts clean.
            if (MediaPlayer.IsValid && MediaPlayer.IsUrlMediaPlayer(out UrlMediaPlayer? urlMediaPlayer))
                urlMediaPlayer.MediaPlayer.AudioFrameTap = null;
        }

        public void Dispose()
        {
            State = VideoState.VsNone;
            HasFailed = false;
            isFrozen = false;
            EnsureAudioFrameBufferIsRemoved();
            MediaPlayer.Dispose(MediaAddress);
            Cts.SafeCancelAndDispose();
        }

        public void UpdateSpatialAudio(bool isSpatial, float? minDistance, float? maxDistance) =>
            MediaPlayer.UpdateSpatialAudio(isSpatial,
                minDistance ?? DEFAULT_SPATIAL_MIN_DISTANCE,
                maxDistance ?? DEFAULT_SPATIAL_MAX_DISTANCE);
    }
}
