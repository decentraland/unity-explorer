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
        ///     Cancels any pending resolve and returns the player to its pool.
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

        private readonly MediaPlayerCustomPool pool;
        private readonly IUrlResolverService resolver;

        private int generation;
        private string? currentUrl;
        private string? failedUrl;
        private CancellationTokenSource? cts;
        private MultiMediaPlayer? player;
        private string? directUrl;
        private bool isLive;
        private bool seekInFlight;
        private float lastSeekAt = float.NegativeInfinity;
        private float volume;
        private Vector3 audioPosition;

        public Texture? Texture => player?.LastTexture();

        public bool RequiresVerticalFlip => player is { } current && current.GetTexureScale.y < 0f;

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
                Release();
                currentUrl = url;

                if (url == failedUrl)
                    return;

                failedUrl = null;
                cts = new CancellationTokenSource();
                ResolveAsync(url, generation, cts.Token).Forget();
                return;
            }

            if (player is not { } current)
            {
                if (directUrl != null && MediaOpenThrottle.CanOpen(now))
                    Open(directUrl, playing, now);

                return;
            }

            if (!current.IsValid)
            {
                Release();
                return;
            }

            if (current.GetLastError() != ErrorCode.None)
            {
                Fail(url);
                Release();
                return;
            }

            bool opened = IsMediaOpened(current);

            if (opened && playing != current.IsPlaying)
            {
                if (playing)
                    current.Play();
                else
                    current.Pause();
            }

            if (seekInFlight && !current.IsSeeking)
                seekInFlight = false;

            if (targetSeconds is { } target && !seekInFlight && now - lastSeekAt >= MIN_SEEK_INTERVAL_SECONDS
                && opened && !current.IsBuffering && !current.IsSeeking
                && PresentationLayout.NeedsSeek(current.CurrentTime, target, isLive))
            {
                seekInFlight = true;
                lastSeekAt = now;
                current.TrySeek(target);
            }
        }

        public void SetVolume(float volume)
        {
            this.volume = volume;
            player?.UpdateVolume(volume);
        }

        public void PlaceAudioAt(Vector3 position)
        {
            audioPosition = position;
            player?.PlaceAt(position);
        }

        public void Pause()
        {
            player?.Pause();
        }

        public void Release()
        {
            generation++;
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
            currentUrl = null;
            seekInFlight = false;
            lastSeekAt = float.NegativeInfinity;

            MultiMediaPlayer? released = player;
            string? releasedUrl = directUrl;
            player = null;
            directUrl = null;

            if (released is { } local && releasedUrl != null)
                local.Dispose(MediaAddress.FromUrlMediaAddress(new UrlMediaAddress(releasedUrl)));
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
                Release();
                return;
            }

            opened.UpdateVolume(volume);
            opened.PlaceAt(audioPosition);
        }

        private void Fail(string? url)
        {
            failedUrl = url;
            ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Presentation client video could not be played: {url}");
        }

        private static bool IsMediaOpened(MultiMediaPlayer media) =>
            media.TryGetAvProPlayer(out MediaPlayer? mediaPlayer) && mediaPlayer != null && mediaPlayer.MediaOpened;
    }
}
