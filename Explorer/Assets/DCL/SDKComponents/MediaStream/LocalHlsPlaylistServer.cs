using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SDKComponents.MediaStream.YouTube;
using System;
using System.Collections.Generic;
using System.Net;
using Utility.Multithreading;

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
        private const string HLS_CONTENT_TYPE = "application/vnd.apple.mpegurl";

        // Entries are tiny strings; the cap only guards against unbounded growth
        // across a long session.
        private const int MAX_ENTRIES = 64;

        private const int PORT_RANGE_START = 41000;
        private const int PORT_RANGE_SIZE = 1000;
        private const int PORT_ATTEMPTS = 16;

        private static readonly object GATE = new ();
        private static readonly Dictionary<string, HlsManifestBuilder.PlaylistSet> ENTRIES = new ();
        private static readonly Dictionary<string, string> TOKEN_BY_KEY = new ();
        private static readonly Queue<(string Key, string Token)> EVICTION_ORDER = new ();

        private static HttpListener? listener;
        private static int port;

        /// <summary>
        ///     Registers a playlist set and returns the loopback URL of its master playlist,
        ///     or null when no listener could be started (all candidate ports taken).
        ///     Re-registering a key replaces its previous entry instead of consuming
        ///     extra capacity.
        /// </summary>
        public static string? TryRegister(string key, in HlsManifestBuilder.PlaylistSet playlists)
        {
            lock (GATE)
            {
                if (!TryEnsureStarted())
                    return null;

                if (TOKEN_BY_KEY.TryGetValue(key, out string? previousToken))
                    ENTRIES.Remove(previousToken);

                var token = Guid.NewGuid().ToString("N");
                ENTRIES[token] = playlists;
                TOKEN_BY_KEY[key] = token;
                EVICTION_ORDER.Enqueue((key, token));

                // Replaced registrations leave dead queue pairs behind; dequeuing them is a
                // no-op on ENTRIES, so keep dequeuing until enough live entries are gone.
                while (ENTRIES.Count > MAX_ENTRIES)
                {
                    (string oldKey, string oldToken) = EVICTION_ORDER.Dequeue();
                    ENTRIES.Remove(oldToken);

                    if (TOKEN_BY_KEY.TryGetValue(oldKey, out string? liveToken) && liveToken == oldToken)
                        TOKEN_BY_KEY.Remove(oldKey);
                }

                return $"http://127.0.0.1:{port.ToString()}/{token}/{HlsManifestBuilder.MASTER_PLAYLIST_NAME}";
            }
        }

        private static bool TryEnsureStarted()
        {
            if (listener is { IsListening: true })
                return true;

            // A dead listener still owns its port; release it before binding a fresh one.
            listener?.Close();
            listener = null;

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
            await DCLTask.SwitchToThreadPool();

            while (local.IsListening)
            {
                HttpListenerContext context;

                // Stop/Close parks GetContextAsync into one of these; treat all as shutdown.
                try { context = await local.GetContextAsync(); }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (InvalidOperationException) { break; }

                try { Serve(context); }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.MEDIA_STREAM); }
            }
        }

        private static void Serve(HttpListenerContext context)
        {
            using HttpListenerResponse response = context.Response;

            // Belt-and-braces on top of the 127.0.0.1-only binding.
            string? playlist = context.Request.IsLocal
                ? Lookup(context.Request.Url?.AbsolutePath)
                : null;

            if (playlist == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            byte[] body = HlsManifestBuilder.HLS_ENCODING.GetBytes(playlist);
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
                       HlsManifestBuilder.MASTER_PLAYLIST_NAME => playlists.Master,
                       HlsManifestBuilder.VIDEO_PLAYLIST_NAME => playlists.Video,
                       HlsManifestBuilder.AUDIO_PLAYLIST_NAME => playlists.Audio,
                       _ => null,
                   };
        }
    }
}
