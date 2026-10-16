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
    ///     <see cref="MediaPlayerCustomPool" />, opening it through <see cref="MediaOpenThrottle" />. Main-thread only.
    /// </summary>
    internal sealed class PresentationUrlVideo : IDisposable
    {
        private const int MAX_LOGGED_URL_LENGTH = 120;

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
        ///     Whether the first row of <see cref="Texture" /> is the top of the image, as in LiveKit frames, so Unity would draw it upside down.
        /// </summary>
        public bool IsTopRowFirst => LivePlayer() is { } current && current.TextureScale.y < 0f;

        public PresentationUrlVideo(MediaPlayerCustomPool pool, IUrlResolverService resolver)
        {
            this.pool = pool;
            this.resolver = resolver;
        }

        public void Dispose()
        {
            Release();
        }

        /// <summary>
        ///     Plays <paramref name="url" />, resolving and opening it first when it changed, and keeps it within two seconds of
        ///     <paramref name="targetSeconds" />. A resolved url that expires before it opens is resolved again.
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
                if (expiresAt > 0f && now >= expiresAt)
                    ReleasePlayer();
                else if (directUrl != null && MediaOpenThrottle.CanOpen(now))
                    Open(directUrl, playing, now);

                return;
            }

            if (LivePlayer() is not { } current)
            {
                ReleasePlayer();
                return;
            }

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
            {
                if (!PresentationLayout.IsRewind(current.CurrentTime, targetSeconds, isLive))
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

            if (targetSeconds is { } target && PresentationLayout.ShouldSeek(current.CurrentTime, target, isLive, current.IsBuffering || current.IsSeeking, now, lastSeekAt))
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

        public void Pause()
        {
            if (LivePlayer() is not { } current)
                return;

            if (IsMediaOpened(current))
                current.Pause();
            else
                DestroyPlayer();
        }

        /// <summary>
        ///     Cancels any pending resolve, destroys the media player and forgets a url that failed, so its next <see cref="Update" /> retries it.
        /// </summary>
        public void Release()
        {
            ReleasePlayer();
            failedUrl = null;
        }

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
            DestroyPlayer();
            directUrl = null;
            expiresAt = 0f;
        }

        private void DestroyPlayer()
        {
            MultiMediaPlayer? released = player;
            player = null;

            if (released is { } local && local.IsUrlMediaPlayer(out UrlMediaPlayer? urlMediaPlayer))
                pool.DestroyMediaPlayer(urlMediaPlayer.MediaPlayer);
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

        private void Open(string url, bool playing, float now)
        {
            var opened = MultiMediaPlayer.FromUrlMediaPlayer(new UrlMediaPlayer(pool.GetOrCreateReusableMediaPlayer(url), pool));
            player = opened;
            MediaOpenThrottle.MarkOpened(now);

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
            string shown = url is { Length: > MAX_LOGGED_URL_LENGTH } ? $"{url[..(MAX_LOGGED_URL_LENGTH - 1)]}…" : url ?? string.Empty;
            ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Presentation client video could not be played: {shown}");
        }

        private static bool IsMediaOpened(MultiMediaPlayer media) =>
            media.IsUrlMediaPlayer(out UrlMediaPlayer? urlMediaPlayer) && urlMediaPlayer.MediaPlayer.MediaOpened;
    }
}
