#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DCL.VideoPlayback
{
    internal readonly struct ProbeStream
    {
        public readonly int Index;
        public readonly bool IsVideo;
        public readonly int Width;
        public readonly int Height;
        public readonly double Fps;

        private ProbeStream(int index, bool isVideo, int width, int height, double fps)
        {
            Index = index;
            IsVideo = isVideo;
            Width = width;
            Height = height;
            Fps = fps;
        }

        public static ProbeStream Video(int index, int width, int height, double fps) =>
            new (index, true, width, height, fps);

        public static ProbeStream Audio(int index) =>
            new (index, false, 0, 0, 0d);
    }

    internal sealed class MediaOpenException : Exception
    {
        public MediaOpenException(string message) : base(message) { }
    }

    /// <summary>
    /// What one open of a source established before any decoding starts: its
    /// duration (zero when the source has none, i.e. live) and which video and
    /// audio streams playback maps. Adaptive manifests expose every rendition as
    /// its own stream, so the pick is made once here and reused by every
    /// subsequent decoder spawn (seeks, rate changes, loop restarts).
    /// </summary>
    internal sealed class MediaProbe
    {
        public const int MAX_WIDTH = 1920;
        public const int MAX_HEIGHT = 1080;
        public const double DEFAULT_FPS = 30d;

        private const int STDERR_LIMIT_LINES = 4000;

        public double Duration { get; }

        public bool IsLive => Duration <= 0d;

        public int VideoStream { get; }

        public int AudioStream { get; }

        public bool HasVideo => VideoStream >= 0;

        public bool HasAudio => AudioStream >= 0;

        public int Width { get; }

        public int Height { get; }

        public double Fps { get; }

        public MediaProbe(double duration, IReadOnlyList<ProbeStream> streams)
        {
            Duration = duration;
            VideoStream = SelectVideo(streams, MAX_WIDTH, MAX_HEIGHT);
            AudioStream = SelectAudio(streams);

            Fps = DEFAULT_FPS;

            for (var i = 0; i < streams.Count; i++)
            {
                if (streams[i].Index != VideoStream) continue;
                Width = streams[i].Width;
                Height = streams[i].Height;
                if (streams[i].Fps > 0d) Fps = streams[i].Fps;
            }
        }

        /// <summary>
        /// Picks the largest video rendition that fits the cap; when every rendition
        /// exceeds it, the smallest one. Returns -1 when the source has no video.
        /// </summary>
        public static int SelectVideo(IReadOnlyList<ProbeStream> streams, int maxWidth, int maxHeight)
        {
            int best = -1;
            long bestArea = -1;
            int smallest = -1;
            long smallestArea = long.MaxValue;

            for (var i = 0; i < streams.Count; i++)
            {
                ProbeStream stream = streams[i];
                if (!stream.IsVideo) continue;

                long area = (long)stream.Width * stream.Height;

                if (stream.Width <= maxWidth && stream.Height <= maxHeight && area > bestArea)
                {
                    best = stream.Index;
                    bestArea = area;
                }

                if (area < smallestArea)
                {
                    smallest = stream.Index;
                    smallestArea = area;
                }
            }

            return best >= 0 ? best : smallest;
        }

        public static int SelectAudio(IReadOnlyList<ProbeStream> streams)
        {
            for (var i = 0; i < streams.Count; i++)
                if (!streams[i].IsVideo)
                    return streams[i].Index;

            return -1;
        }

        /// <summary>
        /// Opens the source with no outputs and parses the input summary. Blocks for
        /// the round-trip to the source, so it runs off the main thread.
        /// </summary>
        public static MediaProbe Run(string ffmpegPath, string source, int timeoutMs)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = FfmpegCommandLine.ProbeArguments(source),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = false,
                RedirectStandardInput = false,
            };

            using var process = new Process { StartInfo = psi };

            try
            {
                if (!process.Start())
                    throw new MediaOpenException($"{ffmpegPath} failed to start");
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new MediaOpenException($"{ffmpegPath} failed to start: {e.Message}");
            }

            var streams = new List<ProbeStream>();
            var duration = 0d;
            var sawDuration = false;
            string lastLine = "ffmpeg reported nothing";
            var lines = 0;

            var stderrThread = new Thread(() =>
            {
                try
                {
                    StreamReader reader = process.StandardError;

                    while (reader.ReadLine() is { } line)
                    {
                        if (++lines > STDERR_LIMIT_LINES) continue;

                        if (FfmpegLogParser.TryParseStream(line, out ProbeStream stream))
                            streams.Add(stream);
                        else if (FfmpegLogParser.TryParseDuration(line, out double seconds))
                        {
                            duration = seconds;
                            sawDuration = true;
                        }
                        else if (line.Length > 0 && !line.StartsWith(" ", StringComparison.Ordinal))
                            lastLine = line;
                    }
                }
                catch (Exception e) when (e is IOException or ObjectDisposedException)
                {
                    lastLine = $"log read failed: {e.Message}";
                }
            }) { IsBackground = true, Name = "VideoPlayback-Linux-Probe" };

            stderrThread.Start();

            if (!process.WaitForExit(timeoutMs))
            {
                TryKill(process);
                stderrThread.Join(1000);
                throw new MediaOpenException($"probe of {source} timed out after {timeoutMs} ms");
            }

            stderrThread.Join(2000);

            if (streams.Count == 0)
                throw new MediaOpenException($"probe of {source} found no streams: {lastLine}");

            if (!sawDuration)
                duration = 0d;

            return new MediaProbe(duration, streams);
        }

        private static void TryKill(Process process)
        {
            try { process.Kill(); }
            catch (InvalidOperationException)
            {
                // Exited between the timeout and the kill.
            }
        }
    }
}
#endif
