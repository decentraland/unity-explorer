using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SDKComponents.MediaStream.YouTube;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Serves synthesized HLS playlists to the UUAV helper over loopback HTTP.
    ///     The helper's sandbox deliberately refuses the <c>file</c> protocol, and inline
    ///     <c>data:</c> playlists are capped by FFmpeg's fixed 4&#160;KB playlist-URL buffer
    ///     (larger ones are silently truncated mid-URL) — a loopback URL is the only carrier
    ///     that is both whitelisted and unbounded. Bound to 127.0.0.1 only; serves nothing
    ///     but the registered playlist strings, each under an unguessable token path.
    /// </summary>
    internal static class LocalHlsPlaylistServer
    {
        private const string MASTER_PLAYLIST_NAME = "master.m3u8";
        private const string HLS_CONTENT_TYPE = "application/vnd.apple.mpegurl";

        // Entries are tiny strings; the cap only guards against unbounded growth across a
        // long session. Oldest-first eviction matches resolution order: an evicted entry's
        // player has long since fetched its playlists (they are read once per OpenMedia).
        private const int MAX_ENTRIES = 64;

        private const int PORT_RANGE_START = 41000;
        private const int PORT_RANGE_SIZE = 1000;
        private const int PORT_ATTEMPTS = 16;

        // HLS spec requires UTF-8 without BOM (RFC 8216 §4).
        private static readonly UTF8Encoding HLS_ENCODING = new (encoderShouldEmitUTF8Identifier: false);

        private static readonly object GATE = new ();
        private static readonly Dictionary<string, HlsManifestBuilder.PlaylistSet> ENTRIES = new ();
        private static readonly Queue<string> EVICTION_ORDER = new ();

        private static HttpListener? listener;
        private static int port;

        /// <summary>
        ///     Registers a playlist set and returns the loopback URL of its master playlist,
        ///     or null when no listener could be started (all candidate ports taken).
        /// </summary>
        public static string? TryRegister(in HlsManifestBuilder.PlaylistSet playlists)
        {
            lock (GATE)
            {
                if (!TryEnsureStarted())
                    return null;

                var token = Guid.NewGuid().ToString("N");
                ENTRIES[token] = playlists;
                EVICTION_ORDER.Enqueue(token);

                while (ENTRIES.Count > MAX_ENTRIES)
                    ENTRIES.Remove(EVICTION_ORDER.Dequeue());

                return $"http://127.0.0.1:{port.ToString()}/{token}/{MASTER_PLAYLIST_NAME}";
            }
        }

        private static bool TryEnsureStarted()
        {
            if (listener is { IsListening: true })
                return true;

            // Deterministic per-attempt ports would collide across Explorer instances;
            // random candidates spread them without any coordination.
            var random = new Random();

            for (var attempt = 0; attempt < PORT_ATTEMPTS; attempt++)
            {
                int candidate = PORT_RANGE_START + random.Next(PORT_RANGE_SIZE);
                var candidateListener = new HttpListener();
                candidateListener.Prefixes.Add($"http://127.0.0.1:{candidate.ToString()}/");

                try
                {
                    candidateListener.Start();
                }
                catch (Exception)
                {
                    candidateListener.Close();
                    continue;
                }

                listener = candidateListener;
                port = candidate;
                RunAsync(candidateListener).Forget();

                ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[{nameof(LocalHlsPlaylistServer)}] Listening on 127.0.0.1:{candidate}");
                return true;
            }

            ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[{nameof(LocalHlsPlaylistServer)}] No free port after {PORT_ATTEMPTS} attempts");
            return false;
        }

        private static async UniTaskVoid RunAsync(HttpListener local)
        {
            await UniTask.SwitchToThreadPool();

            while (local.IsListening)
            {
                HttpListenerContext context;

                // Stop/Close parks GetContextAsync into one of these; treat both as shutdown.
                try { context = await local.GetContextAsync(); }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }

                try { Serve(context); }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.MEDIA_STREAM); }
            }
        }

        private static void Serve(HttpListenerContext context)
        {
            using HttpListenerResponse response = context.Response;

            string? playlist = Lookup(context.Request.Url?.AbsolutePath);

            if (playlist == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            byte[] body = HLS_ENCODING.GetBytes(playlist);
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = HLS_CONTENT_TYPE;
            response.ContentLength64 = body.Length;
            response.OutputStream.Write(body, 0, body.Length);
        }

        private static string? Lookup(string? path)
        {
            // Expected shape: /{token}/{playlist-name}
            if (string.IsNullOrEmpty(path)) return null;

            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return null;

            HlsManifestBuilder.PlaylistSet playlists;

            lock (GATE)
            {
                if (!ENTRIES.TryGetValue(parts[0], out playlists))
                    return null;
            }

            return parts[1] switch
                   {
                       MASTER_PLAYLIST_NAME => playlists.Master,
                       HlsManifestBuilder.VIDEO_PLAYLIST_NAME => playlists.Video,
                       HlsManifestBuilder.AUDIO_PLAYLIST_NAME => playlists.Audio,
                       _ => null,
                   };
        }
    }
}
