using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace UUAV.Tests
{
    /// <summary>
    /// Minimal HTTP file server for the committed fixtures in Tests/Fixtures~,
    /// bound to 127.0.0.1 on a free port. Serving over http exercises the same
    /// whitelisted protocol production uses and works in player builds where
    /// file: is not whitelisted. Built on TcpListener instead of HttpListener
    /// to sidestep Windows URL-ACL registration; implements Range requests
    /// because FFmpeg's http demuxer seeks inside mp4 containers with them.
    /// </summary>
    public sealed class FixtureServer : IDisposable
    {
        private readonly TcpListener listener;
        private readonly string fixturesDirectory;
        private readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();
        private readonly object cacheGate = new object();
        private volatile bool disposed;

        public FixtureServer(string fixturesDirectory)
        {
            this.fixturesDirectory = fixturesDirectory;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var accept = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "UUAV.Tests.FixtureServer",
            };
            accept.Start();
        }

        public int Port { get; }

        public string UrlFor(string fixtureName)
        {
            return $"http://127.0.0.1:{Port}/{fixtureName}";
        }

        /// <summary>
        /// A localhost url on a port nothing listens on: connecting is
        /// actively refused instead of timing out.
        /// </summary>
        public static string ConnectionRefusedUrl()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return $"http://127.0.0.1:{port}/missing.mp4";
        }

        public void Dispose()
        {
            disposed = true;
            listener.Stop();
        }

        private void AcceptLoop()
        {
            while (disposed == false)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch (SocketException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                ThreadPool.QueueUserWorkItem(_ => Serve(client));
            }
        }

        private void Serve(TcpClient client)
        {
            try
            {
                using (client)
                {
                    ServeOneRequest(client.GetStream());
                }
            }
            catch (Exception)
            {
                // the peer aborting mid-transfer is a normal FFmpeg pattern
            }
        }

        // one request per connection (Connection: close); FFmpeg reopens for
        // every range it needs, which small fixtures make cheap
        private void ServeOneRequest(NetworkStream stream)
        {
            string? requestLine = ReadLine(stream);
            if (requestLine == null)
            {
                return;
            }

            string? rangeHeader = null;
            while (ReadLine(stream) is { Length: > 0 } header)
            {
                const string RangePrefix = "Range:";
                if (header.StartsWith(RangePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    rangeHeader = header.Substring(RangePrefix.Length).Trim();
                }
            }

            string[] parts = requestLine.Split(' ');
            if (parts.Length < 2 || (parts[0] != "GET" && parts[0] != "HEAD"))
            {
                WriteResponse(stream, "405 Method Not Allowed", Array.Empty<byte>(), 0, 0, 0, headOnly: true);
                return;
            }

            bool headOnly = parts[0] == "HEAD";
            byte[]? content = Load(Uri.UnescapeDataString(parts[1].TrimStart('/')));
            if (content == null)
            {
                WriteResponse(stream, "404 Not Found", Array.Empty<byte>(), 0, 0, 0, headOnly);
                return;
            }

            long start = 0;
            long end = content.Length - 1;
            bool partial = rangeHeader != null && TryParseRange(rangeHeader, content.Length, ref start, ref end);
            string status = partial ? "206 Partial Content" : "200 OK";
            WriteResponse(stream, status, content, start, end, content.Length, headOnly);
        }

        private byte[]? Load(string fixtureName)
        {
            // fixtures are flat files; anything path-like is not ours to serve
            if (fixtureName.Length == 0 || fixtureName.Contains("..") || fixtureName.Contains("/") || fixtureName.Contains("\\"))
            {
                return null;
            }

            lock (cacheGate)
            {
                if (cache.TryGetValue(fixtureName, out byte[]? bytes))
                {
                    return bytes;
                }

                string path = Path.Combine(fixturesDirectory, fixtureName);
                if (File.Exists(path) == false)
                {
                    return null;
                }

                bytes = File.ReadAllBytes(path);
                cache.Add(fixtureName, bytes);
                return bytes;
            }
        }

        private static bool TryParseRange(string rangeHeader, long totalLength, ref long start, ref long end)
        {
            // "bytes=<start>-[<end>]" is the only form FFmpeg sends
            const string Unit = "bytes=";
            if (rangeHeader.StartsWith(Unit, StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }

            string[] bounds = rangeHeader.Substring(Unit.Length).Split('-');
            if (bounds.Length != 2 || long.TryParse(bounds[0], NumberStyles.None, CultureInfo.InvariantCulture, out long parsedStart) == false)
            {
                return false;
            }

            long parsedEnd = totalLength - 1;
            if (bounds[1].Length > 0 && long.TryParse(bounds[1], NumberStyles.None, CultureInfo.InvariantCulture, out parsedEnd) == false)
            {
                return false;
            }

            if (parsedStart >= totalLength)
            {
                return false;
            }

            start = parsedStart;
            end = Math.Min(parsedEnd, totalLength - 1);
            return true;
        }

        private static void WriteResponse(NetworkStream stream, string status, byte[] content, long start, long end, long totalLength, bool headOnly)
        {
            long bodyLength = content.Length == 0 ? 0 : end - start + 1;

            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 ").Append(status).Append("\r\n");
            sb.Append("Content-Type: application/octet-stream\r\n");
            sb.Append("Accept-Ranges: bytes\r\n");
            sb.Append("Content-Length: ").Append(bodyLength).Append("\r\n");
            if (status.StartsWith("206"))
            {
                sb.Append("Content-Range: bytes ").Append(start).Append('-').Append(end).Append('/').Append(totalLength).Append("\r\n");
            }

            sb.Append("Connection: close\r\n\r\n");

            byte[] header = Encoding.ASCII.GetBytes(sb.ToString());
            stream.Write(header, 0, header.Length);
            if (headOnly == false && bodyLength > 0)
            {
                stream.Write(content, (int)start, (int)bodyLength);
            }

            stream.Flush();
        }

        private static string? ReadLine(NetworkStream stream)
        {
            // byte-at-a-time header reads keep the stream position exact with
            // no reader buffering; headers are tiny so throughput is irrelevant
            var sb = new StringBuilder();
            while (true)
            {
                int value = stream.ReadByte();
                switch (value)
                {
                    case -1:
                        return sb.Length == 0 ? null : sb.ToString();
                    case '\r':
                        continue;
                    case '\n':
                        return sb.ToString();
                    default:
                        sb.Append((char)value);
                        continue;
                }
            }
        }
    }
}
