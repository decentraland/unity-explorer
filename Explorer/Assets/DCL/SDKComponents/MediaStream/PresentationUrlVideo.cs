using Cysharp.Threading.Tasks;
using DCL.AvProSwitch;
using DCL.Diagnostics;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     A presentation video played by the client from its url. Main-thread only.
    /// </summary>
    internal interface IPresentationUrlVideo : IDisposable
    {
        /// <summary>
        ///     The current frame, or <c>null</c> while nothing is decoded.
        /// </summary>
        Texture? Texture { get; }

        /// <summary>
        ///     Whether the first row of <see cref="Texture" /> is the top of the image, as in LiveKit frames, so Unity would draw it upside down.
        /// </summary>
        bool RequiresVerticalFlip { get; }

        /// <summary>
        ///     Plays <paramref name="url" />, resolving and opening it first when it changed, and keeps it within two seconds of
        ///     <paramref name="targetSeconds" />.
        /// </summary>
        void Update(string url, double? targetSeconds, bool playing, float now);

        void SetVolume(float volume);

        void PlaceAudioAt(Vector3 position);

        void Pause();

        /// <summary>
        ///     Cancels any pending resolve, returns the player to its pool and forgets a url that failed, so its next
        ///     <see cref="Update" /> retries it.
        /// </summary>
        void Release();
    }

    /// <summary>
    ///     Resolves a client video url with <see cref="IUrlResolverService" /> and plays it on a pooled media player,
    ///     opening it through <see cref="MediaOpenThrottle" />. Main-thread only.
    /// </summary>
    internal sealed class PresentationUrlVideo : IPresentationUrlVideo
    {
        private const float MIN_SEEK_INTERVAL_SECONDS = 3f;
        private const int MAX_LOGGED_URL_LENGTH = 120;

        private readonly MediaPlayerCustomPool pool;
        private readonly IUrlResolverService resolver;

        private int generation;
        private string? currentUrl;
        private string? failedUrl;
        private CancellationTokenSource? cts;
        private MultiMediaPlayer? player;
        private string? directUrl;
        private bool isLive;
        private bool finished;
        private float lastSeekAt = float.NegativeInfinity;
        private float volume;
        private Vector3 audioPosition;

        public Texture? Texture => LivePlayer()?.LastTexture();

        public bool RequiresVerticalFlip => LivePlayer() is { } current && current.GetTexureScale.y < 0f;

        public PresentationUrlVideo(MediaPlayerCustomPool pool, IUrlResolverService resolver)
        {
            this.pool = pool;
            this.resolver = resolver;
        }

        public void Dispose()
        {
            Release();
        }

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
                if (directUrl != null && MediaOpenThrottle.CanOpen(now))
                    Open(directUrl, playing, now);

                return;
            }

            if (LivePlayer() is not { } current)
                return;

            if (current.GetLastError() != ErrorCode.None)
            {
                Fail(url);
                ReleasePlayer();
                return;
            }

            if (!IsMediaOpened(current))
                return;

            finished |= current.IsFinished;

            if (finished)
                return;

            if (playing != current.IsPlaying)
            {
                if (playing)
                    current.Play();
                else
                    current.Pause();
            }

            if (targetSeconds is { } target && now - lastSeekAt >= MIN_SEEK_INTERVAL_SECONDS
                && !current.IsBuffering && !current.IsSeeking
                && PresentationLayout.NeedsSeek(current.CurrentTime, target, isLive))
            {
                lastSeekAt = now;
                current.TrySeek(target);
            }
        }

        public void SetVolume(float volume)
        {
            this.volume = volume;
            LivePlayer()?.UpdateVolume(volume);
        }

        public void PlaceAudioAt(Vector3 position)
        {
            audioPosition = position;
            LivePlayer()?.PlaceAt(position);
        }

        public void Pause()
        {
            if (LivePlayer() is not { } current)
                return;

            if (IsMediaOpened(current))
                current.Pause();
            else
                ReturnPlayerToPool();
        }

        public void Release()
        {
            ReleasePlayer();
            failedUrl = null;
        }

        private MultiMediaPlayer? LivePlayer()
        {
            if (player is { IsValid: true } current)
                return current;

            if (player != null)
                ReleasePlayer();

            return null;
        }

        private void ReleasePlayer()
        {
            generation++;
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
            currentUrl = null;
            finished = false;
            lastSeekAt = float.NegativeInfinity;
            ReturnPlayerToPool();
            directUrl = null;
        }

        private void ReturnPlayerToPool()
        {
            MultiMediaPlayer? released = player;
            player = null;

            if (released is { } local && directUrl != null)
                local.Dispose(MediaAddress.FromUrlMediaAddress(new UrlMediaAddress(directUrl)));
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
            isLive = resolved.IsLiveStream;
        }

        private void Open(string url, bool playing, float now)
        {
            var opened = MultiMediaPlayer.FromAvProPlayer(new AvProPlayer(pool.GetOrCreateReusableMediaPlayer(url), pool));
            player = opened;
            MediaOpenThrottle.MarkOpened(now);

            if (!opened.OpenMedia(MediaAddress.FromUrlMediaAddress(new UrlMediaAddress(url)), false, playing))
            {
                Fail(currentUrl);
                ReleasePlayer();
                return;
            }

            opened.UpdateVolume(volume);
            opened.PlaceAt(audioPosition);
        }

        private void Fail(string? url)
        {
            failedUrl = url;
            string shown = url is { Length: > MAX_LOGGED_URL_LENGTH } ? $"{url[..(MAX_LOGGED_URL_LENGTH - 1)]}…" : url ?? string.Empty;
            ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Presentation client video could not be played: {shown}");
        }

        private static bool IsMediaOpened(MultiMediaPlayer media) =>
            media.TryGetAvProPlayer(out MediaPlayer? mediaPlayer) && mediaPlayer != null && mediaPlayer.MediaOpened;
    }
}
