using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AOT;
using UnityEngine;

namespace UUAV
{
    /// <summary>
    /// Parent-side fetcher: networkless child FFmpeg RPCs open/read/close here over managed HttpClient
    /// (no native TLS needed), on a per-player thread off Unity's main one; caches one stream per handle so only seeks pay a fresh GET.
    /// </summary>
    internal static class UUAVFetchService
    {
        private static readonly FetchProvider provider = OnFetch;

        internal static TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

        static UUAVFetchService()
        {
            ServicePointManager.DefaultConnectionLimit = Math.Max(
                ServicePointManager.DefaultConnectionLimit, 32);
        }

        private static readonly HttpClient httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        private static readonly ConcurrentDictionary<uint, Handle> handles =
            new ConcurrentDictionary<uint, Handle>();

        private static readonly ConcurrentDictionary<CancellationTokenSource, string> probes =
            new ConcurrentDictionary<CancellationTokenSource, string>();

        private static int nextHandle;

        [ThreadStatic]
        private static byte[]? readBuffer;

        [ThreadStatic]
        private static byte[]? discardBuffer;

        private const int MaxAttempts = 3;

        private const int DiscardChunk = 64 * 1024;

        public static void Register()
        {
            NativeMethods.uuav_set_fetch_provider(provider);
        }

        public static void Unregister()
        {
            NativeMethods.uuav_set_fetch_provider(null);
            foreach (var key in handles.Keys)
            {
                if (handles.TryRemove(key, out var handle))
                {
                    handle.Dispose();
                }
            }
            foreach (var probe in probes.Keys)
            {
                SafeCancel(probe);
            }
        }

        /// <summary>Aborts in-flight fetches for <paramref name="url"/> so the responder thread doesn't wait out the timeout; handle stays usable via a fresh token.</summary>
        public static void CancelUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            foreach (var pair in handles)
            {
                if (pair.Value.Url == url)
                {
                    pair.Value.CancelInFlight();
                }
            }
            foreach (var pair in probes)
            {
                if (pair.Value == url)
                {
                    SafeCancel(pair.Key);
                }
            }
        }

        private static void SafeCancel(CancellationTokenSource cts)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        [MonoPInvokeCallback(typeof(FetchProvider))]
        private static void OnFetch(IntPtr exchangePtr)
        {
            var exchange = Marshal.PtrToStructure<FetchExchange>(exchangePtr);
            try
            {
                switch (exchange.Op)
                {
                    case FetchOp.Open:
                        DoOpen(ref exchange);
                        break;
                    case FetchOp.Read:
                        DoRead(ref exchange);
                        break;
                    case FetchOp.Close:
                        if (handles.TryRemove(exchange.Handle, out var closed))
                        {
                            closed.Dispose();
                        }
                        exchange.Status = FetchStatus.Ok;
                        break;
                    default:
                        exchange.Status = FetchStatus.Err;
                        break;
                }
            }
            catch (Exception e)
            {
                exchange.Status = FetchStatus.Err;
                Debug.LogWarning($"[UUAV] fetch: {e.Message}");
            }

            Marshal.StructureToPtr(exchange, exchangePtr, false);
        }

        private static void DoOpen(ref FetchExchange exchange)
        {
            var url = ReadUrl(exchange.Url, exchange.UrlLen);
            if (string.IsNullOrEmpty(url))
            {
                exchange.Status = FetchStatus.Err;
                return;
            }

            using var probeCts = new CancellationTokenSource();
            probes[probeCts] = url;
            try
            {
                if (!TryProbe(url, probeCts.Token, out var size, out var rangeBlind, out var initial))
                {
                    exchange.Status = FetchStatus.Err;
                    return;
                }

                var handle = unchecked((uint)Interlocked.Increment(ref nextHandle));
                handles[handle] = new Handle(url, size, rangeBlind, initial);
                exchange.OutHandle = handle;
                exchange.Size = rangeBlind ? -1 : size;
                exchange.Status = FetchStatus.Ok;
            }
            finally
            {
                probes.TryRemove(probeCts, out _);
            }
        }

        private static void DoRead(ref FetchExchange exchange)
        {
            if (!handles.TryGetValue(exchange.Handle, out var handle))
            {
                exchange.Status = FetchStatus.Err;
                return;
            }

            var want = (int)Math.Min(exchange.Len, exchange.BufCap);
            if (want <= 0)
            {
                exchange.Status = FetchStatus.Eof;
                return;
            }

            var buffer = readBuffer;
            if (buffer == null || buffer.Length < want)
            {
                buffer = new byte[want];
                readBuffer = buffer;
            }

            var got = handle.CachedRead(exchange.Offset, want, buffer);
            if (got < 0)
            {
                exchange.Status = FetchStatus.Err;
                return;
            }
            if (got == 0)
            {
                exchange.Status = FetchStatus.Eof;
                return;
            }

            Marshal.Copy(buffer, 0, exchange.Buf, got);
            exchange.N = (uint)got;
            exchange.Status = FetchStatus.Ok;
        }

        private static bool TryProbe(
            string url,
            CancellationToken token,
            out long size,
            out bool rangeBlind,
            out HttpResponseMessage? initial)
        {
            size = -1;
            rangeBlind = false;
            initial = null;

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Range = new RangeHeaderValue(0, 0);

                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeout.CancelAfter(RequestTimeout);

                    var response = httpClient
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                        .GetAwaiter()
                        .GetResult();

                    if (!response.IsSuccessStatusCode &&
                        response.StatusCode != HttpStatusCode.RequestedRangeNotSatisfiable)
                    {
                        response.Dispose();
                        if (Backoff(attempt, TransientDelay, token))
                        {
                            return false;
                        }
                        continue;
                    }

                    var contentRange = response.Content.Headers.ContentRange;
                    if (contentRange != null && contentRange.HasLength)
                    {
                        size = contentRange.Length!.Value;
                    }
                    else if (response.StatusCode == HttpStatusCode.OK &&
                             response.Content.Headers.ContentLength.HasValue)
                    {
                        size = response.Content.Headers.ContentLength.Value;
                    }

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        rangeBlind = true;
                        initial = response;
                    }
                    else
                    {
                        response.Dispose();
                    }

                    return true;
                }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested || attempt == MaxAttempts - 1)
                    {
                        Debug.LogWarning($"[UUAV] fetch open {url}: {e.Message}");
                        return false;
                    }
                    if (Backoff(attempt, TransientDelay, token))
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        private const int TransientDelay = 25;
        private const int StarveDelay = 150;

        private static bool Backoff(int attempt, int unit, CancellationToken token)
        {
            return token.WaitHandle.WaitOne(unit * (attempt + 1));
        }

        private static string ReadUrl(IntPtr pointer, uint length)
        {
            if (pointer == IntPtr.Zero || length == 0)
            {
                return string.Empty;
            }

            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, (int)length);
            return Encoding.UTF8.GetString(bytes);
        }

        private enum Position
        {
            Positioned,
            Eof,
            Transient,
            Starved,
        }

        /// <summary>One open resource; cancel/teardown can race reads from any thread, so token and response swap under <c>gate</c>.</summary>
        private sealed class Handle
        {
            public readonly string Url;

            private readonly long size;

            private bool rangeBlind;

            private readonly object gate = new object();
            private CancellationTokenSource cts = new CancellationTokenSource();
            private HttpResponseMessage? response;
            private Stream? stream;
            private ulong streamPos;
            private bool disposed;

            // Cold-start/throughput diagnostics: a reopen is a seek that cost a
            // fresh GET; sequential reads are served from the live stream.
            private int diagReads;
            private int diagReopens;
            private long diagBytes;
            private int diagCalls;
            private int diagHits;
            private const int DiagEvery = 64;

            // 8 MB read-ahead. FFmpeg's 128 KB stop-start reads keep TCP from
            // ramping, so effective throughput collapses far below the link; one
            // long continuous fill lets the connection saturate, then the child's
            // small reads are served from RAM. A seek/first read fills only a
            // small block so the seeky probe doesn't over-fetch.
            private const int CacheBytes = 8 * 1024 * 1024;
            private const int FillChunk = 1 * 1024 * 1024;
            private byte[]? cache;
            private ulong cacheStart;
            private int cacheLen;

            public Handle(string url, long size, bool rangeBlind, HttpResponseMessage? initial)
            {
                Url = url;
                this.size = size;
                this.rangeBlind = rangeBlind;
                if (initial != null)
                {
                    try
                    {
                        Adopt(initial, 0);
                    }
                    catch (Exception)
                    {
                        initial.Dispose();
                    }
                }
            }

            public void Dispose()
            {
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }
                    disposed = true;
                    cts.Cancel();
                    response?.Dispose();
                    response = null;
                    stream = null;
                }
            }

            /// <summary>Cancels + disposes the in-flight response (faults a blocked read), then re-arms with a fresh token.</summary>
            public void CancelInFlight()
            {
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }
                    var old = cts;
                    cts = new CancellationTokenSource();
                    old.Cancel();
                    response?.Dispose();
                }
            }

            /// <summary>Serves from the read-ahead buffer; on a miss fills it with one long continuous read so TCP saturates instead of stop-starting on 128 KB.</summary>
            public int CachedRead(ulong offset, int want, byte[] dst)
            {
                diagCalls++;
                if (cache != null && cacheLen > 0
                    && offset >= cacheStart && offset < cacheStart + (ulong)cacheLen)
                {
                    diagHits++;
                    var avail = (int)(cacheStart + (ulong)cacheLen - offset);
                    var give = Math.Min(want, avail);
                    Buffer.BlockCopy(cache, (int)(offset - cacheStart), dst, 0, give);
                    return give;
                }

                var sequential = cache != null && offset == cacheStart + (ulong)cacheLen;
                var target = sequential ? CacheBytes : FillChunk;
                cache ??= new byte[CacheBytes];

                var filled = 0;
                while (filled < target)
                {
                    var chunk = Math.Min(FillChunk, target - filled);
                    var got = Read(offset + (ulong)filled, chunk, cache, filled);
                    if (got < 0)
                    {
                        if (filled == 0)
                        {
                            return -1;
                        }
                        break;
                    }
                    if (got == 0)
                    {
                        break;
                    }
                    filled += got;
                }

                cacheStart = offset;
                cacheLen = filled;
                if (diagCalls == 1 || diagCalls % 256 == 0)
                {
                    Debug.Log($"[UUAV] fetch-diag calls={diagCalls} hits={diagHits} fills={diagReopens} streamReads={diagReads} bytes={diagBytes} lastFill={filled}");
                }
                if (filled == 0)
                {
                    return 0;
                }
                var served = Math.Min(want, filled);
                Buffer.BlockCopy(cache, 0, dst, 0, served);
                return served;
            }

            public int Read(ulong offset, int len, byte[] buffer, int dstIndex = 0)
            {
                for (var attempt = 0; attempt < MaxAttempts; attempt++)
                {
                    CancellationToken token;
                    lock (gate)
                    {
                        if (disposed)
                        {
                            return -1;
                        }
                        token = cts.Token;
                    }

                    try
                    {
                        switch (EnsurePositioned(offset, token))
                        {
                            case Position.Eof:
                                return 0;
                            case Position.Starved:
                                if (Backoff(attempt, StarveDelay, token))
                                {
                                    return -1;
                                }
                                continue;
                            case Position.Transient:
                                if (Backoff(attempt, TransientDelay, token))
                                {
                                    return -1;
                                }
                                continue;
                        }

                        var total = 0;
                        while (total < len)
                        {
                            var read = ReadChunk(buffer, dstIndex + total, len - total, token);
                            if (read <= 0)
                            {
                                break;
                            }
                            total += read;
                            if (size < 0)
                            {
                                break;
                            }
                        }

                        if (total == 0)
                        {
                            Teardown();
                            if (size >= 0 && offset >= (ulong)size)
                            {
                                return 0;
                            }
                            if (Backoff(attempt, TransientDelay, token))
                            {
                                return -1;
                            }
                            continue;
                        }

                        lock (gate)
                        {
                            streamPos = offset + (ulong)total;
                        }
                        diagReads++;
                        diagBytes += total;
                        if (diagReads == 1 || diagReads % DiagEvery == 0)
                        {
                            Debug.Log($"[UUAV] fetch-diag reads={diagReads} reopens={diagReopens} bytes={diagBytes}");
                        }
                        return total;
                    }
                    catch (Exception e)
                    {
                        Teardown();
                        if (token.IsCancellationRequested)
                        {
                            return -1;
                        }
                        if (attempt == MaxAttempts - 1)
                        {
                            Debug.LogWarning($"[UUAV] fetch read {Url}: {e.Message}");
                            return -1;
                        }
                        if (Backoff(attempt, TransientDelay, token))
                        {
                            return -1;
                        }
                    }
                }

                return -1;
            }

            private Position EnsurePositioned(ulong offset, CancellationToken token)
            {
                lock (gate)
                {
                    if (stream != null && streamPos == offset)
                    {
                        return Position.Positioned;
                    }
                }
                var jump = stream == null ? -1L : (long)offset - (long)streamPos;
                diagReopens++;
                Debug.Log($"[UUAV] fetch-diag reopen={diagReopens} at read={diagReads} off={offset} jump={jump}");
                Teardown();

                using var request = new HttpRequestMessage(HttpMethod.Get, Url);
                if (!rangeBlind)
                {
                    request.Headers.Range = new RangeHeaderValue((long)offset, null);
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(RequestTimeout);

                var reply = httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                    .GetAwaiter()
                    .GetResult();

                if (reply.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    reply.Dispose();
                    return size >= 0 ? Position.Eof : Position.Starved;
                }
                if (!reply.IsSuccessStatusCode)
                {
                    reply.Dispose();
                    return Position.Transient;
                }

                if (reply.StatusCode == HttpStatusCode.PartialContent)
                {
                    var from = reply.Content.Headers.ContentRange?.From;
                    if (from.HasValue && (ulong)from.Value != offset)
                    {
                        reply.Dispose();
                        Debug.LogWarning(
                            $"[UUAV] fetch {Url}: 206 started at {from.Value}, wanted {offset}"
                        );
                        return Position.Transient;
                    }
                    Adopt(reply, offset);
                    return Position.Positioned;
                }

                if (reply.StatusCode == HttpStatusCode.OK)
                {
                    rangeBlind = true;
                    Adopt(reply, 0);
                    return SkipTo(offset, token) ? Position.Positioned : Position.Eof;
                }

                reply.Dispose();
                return Position.Transient;
            }

            private void Adopt(HttpResponseMessage reply, ulong position)
            {
                var body = reply.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                lock (gate)
                {
                    response = reply;
                    stream = body;
                    streamPos = position;
                }
            }

            private bool SkipTo(ulong offset, CancellationToken token)
            {
                var discard = discardBuffer ??= new byte[DiscardChunk];
                ulong position;
                lock (gate)
                {
                    position = streamPos;
                }

                while (position < offset)
                {
                    var step = (int)Math.Min((ulong)discard.Length, offset - position);
                    var read = ReadChunk(discard, 0, step, token);
                    if (read <= 0)
                    {
                        Teardown();
                        return false;
                    }
                    position += (ulong)read;
                    lock (gate)
                    {
                        streamPos = position;
                    }
                }

                return true;
            }

            private int ReadChunk(byte[] buffer, int index, int count, CancellationToken token)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(RequestTimeout);
                using var abort = timeout.Token.Register(AbortStream);
                return stream!
                    .ReadAsync(buffer, index, count, timeout.Token)
                    .GetAwaiter()
                    .GetResult();
            }

            private void AbortStream()
            {
                lock (gate)
                {
                    response?.Dispose();
                }
            }

            private void Teardown()
            {
                lock (gate)
                {
                    response?.Dispose();
                    response = null;
                    stream = null;
                    streamPos = 0;
                }
            }
        }
    }
}
