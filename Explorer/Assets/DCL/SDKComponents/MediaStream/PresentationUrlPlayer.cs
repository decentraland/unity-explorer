using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using UUAV.Compat;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Resolves a client video url with <see cref="IUrlResolverService" /> and plays it on a media player taken from
    ///     <see cref="MediaPlayerCustomPool" /> under <see cref="POOL_KEY" />, parking it there again on release. Main-thread only.
    /// </summary>
    internal sealed class PresentationUrlPlayer : IDisposable
    {
        private const int MAX_LOGGED_URL_LENGTH = 120;
        private const string POOL_KEY = "presentation-client-video";
        private const string YOUTUBE_WATCH_PREFIX = "https://www.youtube.com/watch?v=";
        private const double SEEK_THRESHOLD_SECONDS = 2.0;
        private const float MIN_SEEK_INTERVAL_SECONDS = 3f;

        private readonly MediaPlayerCustomPool pool;
        private readonly IUrlResolverService resolver;

        private int generation;
        private string? currentUrl;
        private string? failedUrl;
        private CancellationTokenSource? cts;
        private MultiMediaPlayer? player;
        private string? directUrl;
        private float expiresAt;
        private bool isLive;
        private bool finished;
        private float lastSeekAt = float.NegativeInfinity;
        private float targetVolume;
        private Vector3 audioPosition;

        /// <summary>
        ///     The current frame, or <c>null</c> while nothing is decoded.
        /// </summary>
        public Texture? Texture => LivePlayer()?.LastTexture();

        /// <summary>
        ///     Whether <see cref="Texture" /> has Unity's bottom-left origin, so the compositor must flip it to match LiveKit frames;
        ///     <c>false</c> while there is no media player.
        /// </summary>
        public bool RequiresCompositorFlip => LivePlayer() is { } current && current.TextureScale.y >= 0f;

        public PresentationUrlPlayer(MediaPlayerCustomPool pool, IUrlResolverService resolver)
        {
            this.pool = pool;
            this.resolver = resolver;
        }

        public void Dispose()
        {
            Release();
        }

        /// <summary>
        ///     Plays <paramref name="url" />, resolving and opening it first when it changed, and seeks it to
        ///     <paramref name="targetSeconds" /> when <see cref="ShouldSeek" /> holds. A resolved url that expires before it
        ///     opens, or errors after it expired, is resolved again.
        /// </summary>
        public void Update(string url, double? targetSeconds, bool playing, float now)
        {
            if (url != currentUrl)
            {
                ReleasePlayer();
                currentUrl = url;

                if (url == failedUrl)
                    return;

                failedUrl = null;
                cts = new CancellationTokenSource();
                ResolveAsync(url, generation, cts.Token).Forget();
                return;
            }

            if (player == null)
            {
                if (IsExpired(expiresAt, now))
                    ReleasePlayer();
                else if (directUrl != null)
                    Open(directUrl, playing);

                return;
            }

            if (LivePlayer() is not { } current)
            {
                ReleasePlayer();
                return;
            }

            if (current.GetLastError() != ErrorCode.None)
            {
                if (!IsExpired(expiresAt, now))
                    Fail(url);

                ReleasePlayer();
                return;
            }

            if (!IsMediaOpened(current) || current.IsBuffering)
                return;

            finished |= current.IsFinished;

            if (finished)
            {
                if (!IsRewind(current.CurrentTime, targetSeconds, isLive))
                    return;

                finished = false;
            }

            if (playing != current.IsPlaying)
            {
                if (playing)
                    current.Play();
                else
                    current.Pause();
            }

            if (targetSeconds is { } target && ShouldSeek(current.CurrentTime, target, isLive, current.IsSeeking, now, lastSeekAt))
            {
                lastSeekAt = now;
                current.TrySeek(target);
            }
        }

        public void SetVolume(float volume)
        {
            targetVolume = volume;
            LivePlayer()?.UpdateVolume(volume);
        }

        public void PlaceAudioAt(Vector3 position)
        {
            audioPosition = position;
            LivePlayer()?.PlaceAt(position);
        }

        /// <summary>
        ///     Pauses the video, or parks its media player when the media isn't open yet.
        /// </summary>
        public void Pause()
        {
            if (LivePlayer() is not { } current)
                return;

            if (IsMediaOpened(current))
                current.Pause();
            else
                ParkPlayer();
        }

        /// <summary>
        ///     Cancels any pending resolve, parks the media player and forgets a url that failed.
        /// </summary>
        public void Release()
        {
            ReleasePlayer();
            failedUrl = null;
        }

        /// <summary>
        ///     Whether the player has drifted more than <see cref="SEEK_THRESHOLD_SECONDS" /> from <paramref name="targetSeconds" />. A live
        ///     stream never needs a seek.
        /// </summary>
        internal static bool NeedsSeek(double currentSeconds, double? targetSeconds, bool isLive) =>
            !isLive && targetSeconds is { } target && Math.Abs(currentSeconds - target) > SEEK_THRESHOLD_SECONDS;

        /// <summary>
        ///     Whether <paramref name="targetSeconds" /> is more than <see cref="SEEK_THRESHOLD_SECONDS" /> behind <paramref name="currentSeconds" />.
        ///     A live stream never rewinds.
        /// </summary>
        internal static bool IsRewind(double currentSeconds, double? targetSeconds, bool isLive) =>
            targetSeconds < currentSeconds && NeedsSeek(currentSeconds, targetSeconds, isLive);

        /// <summary>
        ///     Whether a player that last sought at <paramref name="lastSeekAt" /> seeks now: it isn't busy buffering or seeking, at least
        ///     <see cref="MIN_SEEK_INTERVAL_SECONDS" /> passed since that seek, and <see cref="NeedsSeek" /> holds.
        /// </summary>
        internal static bool ShouldSeek(double currentSeconds, double? targetSeconds, bool isLive, bool isBusy, float now, float lastSeekAt) =>
            !isBusy && now - lastSeekAt >= MIN_SEEK_INTERVAL_SECONDS && NeedsSeek(currentSeconds, targetSeconds, isLive);

        /// <summary>
        ///     Whether a resolved url with <paramref name="expiresAt" /> has expired at <paramref name="now" />; one with no expiry never does.
        /// </summary>
        internal static bool IsExpired(float expiresAt, float now) =>
            expiresAt > 0f && now >= expiresAt;

        private MultiMediaPlayer? LivePlayer() =>
            player is { IsValid: true } current ? current : null;

        private void ReleasePlayer()
        {
            generation++;
            cts.SafeCancelAndDispose();
            cts = null;
            currentUrl = null;
            finished = false;
            lastSeekAt = float.NegativeInfinity;
            ParkPlayer();
            directUrl = null;
            expiresAt = 0f;
            isLive = false;
        }

        private void ParkPlayer()
        {
            MultiMediaPlayer? released = player;
            player = null;

            if (released is { } local && local.IsUrlMediaPlayer(out UrlMediaPlayer? urlMediaPlayer))
                pool.ReleaseMediaPlayer(POOL_KEY, urlMediaPlayer.MediaPlayer);
        }

        private async UniTaskVoid ResolveAsync(string url, int gen, CancellationToken ct)
        {
            ResolvedMediaUrl resolved;

            try { resolved = await resolver.ResolveAsync(url, new ReportData(ReportCategory.MEDIA_STREAM), ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                if (gen == generation)
                    failedUrl = url;

                ReportHub.LogException(e, new ReportData(ReportCategory.MEDIA_STREAM));
                return;
            }

            if (gen != generation || ct.IsCancellationRequested)
                return;

            if (!resolved.IsReachable)
            {
                Fail(url);
                return;
            }

            directUrl = resolved.DirectUrl;
            expiresAt = resolved.ExpiresAtRealtimeSinceStartup;
            isLive = resolved.IsLiveStream;
        }

        private void Open(string url, bool playing)
        {
            var opened = MultiMediaPlayer.FromUrlMediaPlayer(new UrlMediaPlayer(pool.GetOrCreateReusableMediaPlayer(POOL_KEY), pool));
            player = opened;

            if (!opened.OpenMedia(MediaAddress.FromUrlMediaAddress(new UrlMediaAddress(url)), false, playing))
            {
                Fail(currentUrl);
                ReleasePlayer();
                return;
            }

            opened.UpdateVolume(targetVolume);
            opened.PlaceAt(audioPosition);
        }

        private void Fail(string? url)
        {
            failedUrl = url;
            string shown = url ?? string.Empty;
            int query = shown.StartsWith(YOUTUBE_WATCH_PREFIX, StringComparison.Ordinal) ? -1 : shown.IndexOf('?');

            if (query >= 0)
                shown = shown[..query];

            if (shown.Length > MAX_LOGGED_URL_LENGTH)
                shown = $"{shown[..(MAX_LOGGED_URL_LENGTH - 1)]}…";

            ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Presentation client video could not be played: {shown}");
        }

        private static bool IsMediaOpened(MultiMediaPlayer media) =>
            media.IsUrlMediaPlayer(out UrlMediaPlayer? urlMediaPlayer) && urlMediaPlayer.MediaPlayer.MediaOpened;
    }
}
