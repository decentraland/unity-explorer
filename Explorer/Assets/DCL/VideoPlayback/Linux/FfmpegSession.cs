#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DCL.VideoPlayback
{
    internal sealed class VideoFrame
    {
        /// <summary>Bottom-up 32-bit pixels in the session's channel order (<see cref="FfmpegSession.RgbaFrames"/>).</summary>
        public byte[] Pixels;
        public int Width;
        public int Height;

        /// <summary>Seconds since the session's first frame; the presenter adds the session's start time.</summary>
        public double Time;

        public VideoFrame(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height * BmpFrame.BYTES_PER_PIXEL];
        }
    }

    /// <summary>
    /// One decoder process together with everything it feeds: the two named
    /// pipes, the reader threads, the bounded queue of decoded frames and the
    /// audio ring. A session starts at one media position and runs to the end
    /// of the stream; repositioning, rate changes and loop restarts are new
    /// sessions. Producers block when their buffer is full, which back-pressures
    /// the decoder through the pipes: a paused player therefore really stops
    /// decoding instead of racing ahead.
    /// </summary>
    internal sealed class FfmpegSession : IDisposable
    {
        private const int AUDIO_READ_BUFFER_BYTES = 16 * 1024;
        private const int FRAME_QUEUE_BUDGET_BYTES = 48 * 1024 * 1024;
        private const int FRAME_QUEUE_MIN = 3;
        private const int FRAME_QUEUE_MAX = 24;
        private const double AUDIO_RING_SECONDS = 0.5;
        private const int SHOWINFO_WAIT_MS = 250;
        private const int CONSUMER_STALL_MS = 500;
        private const int IDLE_POLL_MS = 5;
        private const int FULL_BUFFER_POLL_MS = 2;
        private const int THREAD_JOIN_MS = 1500;
        private const int KILL_WAIT_MS = 500;
        private const int LOG_TAIL_LINES = 8;

        private readonly Process process;
        private readonly string videoFifo;
        private readonly string audioFifo;
        private readonly PosixFifo.Reader? videoPipe;
        private readonly PosixFifo.Reader? audioPipe;
        private readonly Thread? videoThread;
        private readonly Thread? audioThread;
        private readonly Thread stderrThread;
        private readonly ConcurrentQueue<FfmpegLogParser.ShowInfo> showInfo = new ();
        private readonly Queue<string> logTail = new ();
        private readonly object queueLock = new ();
        private readonly Queue<VideoFrame> frames = new ();
        private readonly Stack<VideoFrame> pool = new ();

        private int queueCapacity = FRAME_QUEUE_MIN;
        private int frameBytes;

        private volatile bool disposed;
        private volatile bool paused;
        private volatile bool videoEof;
        private volatile bool audioEof;
        private volatile bool framesArrived;
        private volatile bool audioArrived;
        private volatile bool consumerStalled;
        private volatile string framingError = string.Empty;

        public double StartTime { get; }

        public float Rate { get; }

        public bool HasVideo { get; }

        /// <summary>True when frames are handed out as RGBA; otherwise they keep the decoder's BGRA order.</summary>
        public bool RgbaFrames { get; }

        public bool HasAudio => Audio != null;

        public double FrameInterval { get; }

        public AudioRing? Audio { get; }

        /// <summary>Set by the presenter once the first frame (or first audio) of the session reached the output.</summary>
        public bool PresentedAny { get; set; }

        /// <summary>Set by the presenter after reporting a non-zero exit, so the report happens once.</summary>
        public bool FailureReported { get; set; }

        /// <summary>While true the producers hold on a full buffer indefinitely instead of dropping.</summary>
        public bool Paused
        {
            get => paused;
            set => paused = value;
        }

        public bool Exited
        {
            get
            {
                try { return process.HasExited; }
                catch (InvalidOperationException) { return true; }
            }
        }

        public int ExitCode
        {
            get
            {
                try { return process.HasExited ? process.ExitCode : 0; }
                catch (InvalidOperationException) { return -1; }
            }
        }

        public bool VideoEof => videoEof;

        public bool AudioEof => audioEof;

        public bool AnyOutput => framesArrived || audioArrived;

        /// <summary>Non-empty once the decoder produced bytes the BMP framing could not follow; playback cannot continue.</summary>
        public string FramingError => framingError;

        public int QueuedFrames
        {
            get
            {
                lock (queueLock) { return frames.Count; }
            }
        }

        public bool IsVideoStarved => HasVideo && !videoEof && QueuedFrames == 0;

        /// <summary>
        /// Everything the decoder produced has been handed out: the process is
        /// gone, the frame queue is empty and the audio ring is drained (or nobody
        /// is draining it).
        /// </summary>
        public bool IsExhausted =>
            IsDrainedExceptAudio && (Audio == null || Audio.IsEmpty || consumerStalled);

        /// <summary>The decoder is gone and every video frame was handed out; only the audio ring may still hold samples.</summary>
        public bool IsDrainedExceptAudio =>
            Exited
            && (!HasVideo || (videoEof && QueuedFrames == 0))
            && (Audio == null || audioEof);

        public string LogTail
        {
            get
            {
                lock (logTail) { return string.Join("\n", logTail); }
            }
        }

        private FfmpegSession(SpawnAttempt attempt, Process process, MediaProbe probe, double startTime, float rate, int sampleRate, int channels, bool rgbaFrames)
        {
            this.process = process;
            videoFifo = attempt.VideoFifo;
            audioFifo = attempt.AudioFifo;
            videoPipe = attempt.VideoPipe;
            audioPipe = attempt.AudioPipe;

            StartTime = startTime;
            Rate = rate;
            HasVideo = videoPipe != null;
            RgbaFrames = rgbaFrames;
            FrameInterval = probe.Fps > 0d ? 1d / probe.Fps : 1d / MediaProbe.DEFAULT_FPS;

            stderrThread = new Thread(StderrLoop) { IsBackground = true, Name = "VideoPlayback-Linux-Log" };
            stderrThread.Start();

            if (videoPipe is { } video)
            {
                videoThread = new Thread(() => VideoLoop(video)) { IsBackground = true, Name = "VideoPlayback-Linux-Video" };
                videoThread.Start();
            }

            if (audioPipe is { } pcm)
            {
                var ring = new AudioRing(channels, sampleRate, AUDIO_RING_SECONDS);
                Audio = ring;
                audioThread = new Thread(() => AudioLoop(pcm, ring)) { IsBackground = true, Name = "VideoPlayback-Linux-Audio" };
                audioThread.Start();
            }
        }

        /// <summary>
        /// Creates the pipes, launches the decoder at <paramref name="startTime"/> and
        /// starts draining. Blocks only on process creation. Frames are delivered in
        /// the decoder's BGRA order unless <paramref name="rgbaFrames"/> asks for a
        /// swizzle to RGBA.
        /// </summary>
        public static FfmpegSession Spawn(string ffmpegPath, string source, MediaProbe probe, double startTime, float rate,
            int sampleRate, int channels, bool rgbaFrames)
        {
            using var attempt = new SpawnAttempt();

            try
            {
                if (probe.HasVideo) attempt.VideoFifo = PosixFifo.Create("bgra");
                if (probe.HasAudio && channels > 0) attempt.AudioFifo = PosixFifo.Create("f32");

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = FfmpegCommandLine.PlaybackArguments(source, probe, startTime, rate, sampleRate, channels,
                        attempt.VideoFifo.Length > 0 ? attempt.VideoFifo : null, attempt.AudioFifo.Length > 0 ? attempt.AudioFifo : null),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false,
                    RedirectStandardInput = false,
                };

                var process = new Process { StartInfo = psi };
                attempt.Process = process;

                try
                {
                    if (!process.Start())
                        throw new MediaOpenException($"{ffmpegPath} failed to start");
                }
                catch (System.ComponentModel.Win32Exception e)
                {
                    throw new MediaOpenException($"{ffmpegPath} failed to start: {e.Message}");
                }

                if (attempt.VideoFifo.Length > 0) attempt.VideoPipe = new PosixFifo.Reader(attempt.VideoFifo);
                if (attempt.AudioFifo.Length > 0) attempt.AudioPipe = new PosixFifo.Reader(attempt.AudioFifo);

                var session = new FfmpegSession(attempt, process, probe, startTime, rate, sampleRate, channels, rgbaFrames);
                attempt.Committed = true;
                return session;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                throw new MediaOpenException(e.Message);
            }
        }

        /// <summary>Removes and returns the head frame regardless of its time; null when the queue is empty.</summary>
        public VideoFrame? TakeNext()
        {
            lock (queueLock)
            {
                if (frames.Count == 0) return null;
                VideoFrame frame = frames.Dequeue();
                Monitor.PulseAll(queueLock);
                return frame;
            }
        }

        /// <summary>
        /// Removes every frame due at or before <paramref name="sessionTime"/>
        /// (seconds relative to the session start) and returns the latest of
        /// them, recycling the ones it skipped; null when none is due.
        /// </summary>
        public VideoFrame? TakeLatestDue(double sessionTime)
        {
            lock (queueLock)
            {
                if (frames.Count == 0 || frames.Peek().Time > sessionTime) return null;

                VideoFrame latest = frames.Dequeue();

                while (frames.Count > 0 && frames.Peek().Time <= sessionTime)
                {
                    Return(latest);
                    latest = frames.Dequeue();
                }

                Monitor.PulseAll(queueLock);
                return latest;
            }
        }

        /// <summary>Hands a presented frame's buffer back for reuse.</summary>
        public void Recycle(VideoFrame frame)
        {
            lock (queueLock) { Return(frame); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            lock (queueLock) { Monitor.PulseAll(queueLock); }

            Kill(process);
            JoinQuietly(videoThread);
            JoinQuietly(audioThread);
            JoinQuietly(stderrThread);

            videoPipe?.Dispose();
            audioPipe?.Dispose();

            if (videoFifo.Length > 0) PosixFifo.Delete(videoFifo);
            if (audioFifo.Length > 0) PosixFifo.Delete(audioFifo);

            process.Dispose();

            lock (queueLock)
            {
                frames.Clear();
                pool.Clear();
            }
        }

        /// <summary>Resources of a spawn in progress; released on dispose unless the session took them over.</summary>
        private sealed class SpawnAttempt : IDisposable
        {
            public string VideoFifo = string.Empty;
            public string AudioFifo = string.Empty;
            public Process? Process;
            public PosixFifo.Reader? VideoPipe;
            public PosixFifo.Reader? AudioPipe;
            public bool Committed;

            public void Dispose()
            {
                if (Committed) return;

                VideoPipe?.Dispose();
                AudioPipe?.Dispose();

                if (Process is { } process)
                {
                    Kill(process);
                    process.Dispose();
                }

                if (VideoFifo.Length > 0) PosixFifo.Delete(VideoFifo);
                if (AudioFifo.Length > 0) PosixFifo.Delete(AudioFifo);
            }
        }

        private static void Kill(Process process)
        {
            try
            {
                if (process.HasExited) return;
                process.Kill();
                process.WaitForExit(KILL_WAIT_MS);
            }
            catch (InvalidOperationException)
            {
                // Exited between the check and the kill.
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Already reaped by the runtime.
            }
        }

        private static void JoinQuietly(Thread? thread)
        {
            if (thread is { IsAlive: true } alive)
                alive.Join(THREAD_JOIN_MS);
        }

        private void Return(VideoFrame frame)
        {
            if (frame.Pixels.Length == frameBytes && pool.Count <= queueCapacity)
                pool.Push(frame);
        }

        private void Note(string line)
        {
            lock (logTail)
            {
                if (logTail.Count >= LOG_TAIL_LINES) logTail.Dequeue();
                logTail.Enqueue(line);
            }
        }

        private void VideoLoop(PosixFifo.Reader pipe)
        {
            var header = new byte[BmpFrame.HEADER_BYTES];
            byte[] pixels = Array.Empty<byte>();
            var frameIndex = 0;
            double firstTime = double.NaN;
            var lastTime = 0d;

            try
            {
                while (!disposed)
                {
                    if (!ReadExactly(pipe, header, header.Length)) break;

                    if (!BmpFrame.TryParseHeader(header, out BmpFrame.Header bmp, out string error))
                    {
                        framingError = error;
                        Note($"video framing lost: {error}");
                        break;
                    }

                    int skip = bmp.PixelOffset - BmpFrame.HEADER_BYTES;
                    if (skip > 0 && !Skip(pipe, skip)) break;

                    if (pixels.Length < bmp.PixelBytes)
                        pixels = new byte[bmp.PixelBytes];

                    if (!ReadExactly(pipe, pixels, bmp.PixelBytes)) break;

                    double time = ResolveFrameTime(frameIndex, lastTime);
                    if (double.IsNaN(firstTime)) firstTime = time;
                    time -= firstTime;
                    if (time < lastTime) time = lastTime;
                    lastTime = time;

                    VideoFrame frame = Rent(bmp.Width, bmp.Height);

                    if (RgbaFrames) BmpFrame.ConvertToRgba(pixels, bmp, frame.Pixels);
                    else BmpFrame.CopyBgra(pixels, bmp, frame.Pixels);

                    frame.Time = time;

                    if (!Enqueue(frame)) break;

                    frameIndex++;
                    framesArrived = true;
                }
            }
            catch (ThreadAbortException)
            {
                // Domain reload; nothing left to drain.
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                if (!disposed) Note($"video pipe closed: {e.Message}");
            }

            videoEof = true;
        }

        private double ResolveFrameTime(int frameIndex, double lastTime)
        {
            var waitedMs = 0;

            while (!disposed)
            {
                if (showInfo.TryPeek(out FfmpegLogParser.ShowInfo info))
                {
                    if (info.Index < frameIndex)
                    {
                        showInfo.TryDequeue(out _);
                        continue;
                    }

                    if (info.Index == frameIndex)
                    {
                        showInfo.TryDequeue(out _);
                        return info.HasTime ? info.Time : lastTime + FrameInterval;
                    }

                    break;
                }

                if (waitedMs >= SHOWINFO_WAIT_MS) break;
                Thread.Sleep(1);
                waitedMs++;
            }

            return lastTime + FrameInterval;
        }

        private VideoFrame Rent(int width, int height)
        {
            int bytes = width * height * BmpFrame.BYTES_PER_PIXEL;

            lock (queueLock)
            {
                if (bytes != frameBytes)
                {
                    frameBytes = bytes;
                    pool.Clear();
                    queueCapacity = Math.Max(FRAME_QUEUE_MIN, Math.Min(FRAME_QUEUE_MAX, FRAME_QUEUE_BUDGET_BYTES / bytes));
                }

                if (pool.Count > 0)
                {
                    VideoFrame reused = pool.Pop();
                    reused.Width = width;
                    reused.Height = height;
                    return reused;
                }
            }

            return new VideoFrame(width, height);
        }

        private bool Enqueue(VideoFrame frame)
        {
            lock (queueLock)
            {
                while (frames.Count >= queueCapacity && !disposed)
                    Monitor.Wait(queueLock, IDLE_POLL_MS * 4);

                if (disposed) return false;

                frames.Enqueue(frame);
                return true;
            }
        }

        private void AudioLoop(PosixFifo.Reader pipe, AudioRing ring)
        {
            int frameBytesPcm = sizeof(float) * ring.Channels;
            var bytes = new byte[AUDIO_READ_BUFFER_BYTES];
            var floats = new float[AUDIO_READ_BUFFER_BYTES / sizeof(float)];
            var carry = 0;
            long lastConsumed = ring.ConsumedFrames;
            var stall = new Stopwatch();

            try
            {
                while (!disposed)
                {
                    int read = pipe.Read(bytes, carry, bytes.Length - carry);

                    if (read <= 0)
                    {
                        if (Exited) break;
                        Thread.Sleep(IDLE_POLL_MS);
                        continue;
                    }

                    int total = carry + read;
                    int usable = total - (total % frameBytesPcm);
                    Buffer.BlockCopy(bytes, 0, floats, 0, usable);
                    carry = total - usable;
                    if (carry > 0) Buffer.BlockCopy(bytes, usable, bytes, 0, carry);

                    int count = usable / sizeof(float);
                    var offset = 0;
                    audioArrived = true;

                    while (offset < count && !disposed)
                    {
                        offset += ring.Write(floats, offset, count - offset);
                        if (offset >= count) break;

                        if (paused)
                        {
                            stall.Reset();
                            Thread.Sleep(IDLE_POLL_MS);
                            continue;
                        }

                        long consumed = ring.ConsumedFrames;

                        if (consumed != lastConsumed)
                        {
                            lastConsumed = consumed;
                            consumerStalled = false;
                            stall.Restart();
                            Thread.Sleep(FULL_BUFFER_POLL_MS);
                            continue;
                        }

                        if (!stall.IsRunning) stall.Restart();

                        if (stall.ElapsedMilliseconds > CONSUMER_STALL_MS)
                        {
                            // Nobody is draining the ring (no audio device, or the
                            // mixer stopped calling). Drop the remainder so the
                            // decoder keeps feeding video instead of blocking here.
                            consumerStalled = true;
                            break;
                        }

                        Thread.Sleep(FULL_BUFFER_POLL_MS);
                    }

                    long consumedNow = ring.ConsumedFrames;

                    if (consumedNow != lastConsumed)
                    {
                        lastConsumed = consumedNow;
                        consumerStalled = false;
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // Domain reload; nothing left to drain.
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                if (!disposed) Note($"audio pipe closed: {e.Message}");
            }

            audioEof = true;
        }

        private void StderrLoop()
        {
            try
            {
                StreamReader reader = process.StandardError;

                while (!disposed && reader.ReadLine() is { } line)
                {
                    if (FfmpegLogParser.TryParseShowInfo(line, out FfmpegLogParser.ShowInfo info))
                    {
                        showInfo.Enqueue(info);
                        continue;
                    }

                    if (line.Length == 0 || line.IndexOf("Parsed_showinfo", StringComparison.Ordinal) >= 0) continue;

                    Note(line);
                }
            }
            catch (ThreadAbortException)
            {
                // Domain reload; nothing left to drain.
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
            {
                if (!disposed) Note($"decoder log closed: {e.Message}");
            }
        }

        /// <summary>Fills <paramref name="buffer"/> with exactly <paramref name="count"/> bytes; false once the writer is gone for good.</summary>
        private bool ReadExactly(PosixFifo.Reader pipe, byte[] buffer, int count)
        {
            var offset = 0;

            while (offset < count)
            {
                if (disposed) return false;

                int read = pipe.Read(buffer, offset, count - offset);

                if (read <= 0)
                {
                    // Zero bytes means either no writer yet or the writer closed:
                    // only a dead process settles it.
                    if (Exited) return false;
                    Thread.Sleep(IDLE_POLL_MS);
                    continue;
                }

                offset += read;
            }

            return true;
        }

        private bool Skip(PosixFifo.Reader pipe, int count)
        {
            var scratch = new byte[Math.Min(count, 4096)];

            while (count > 0)
            {
                int take = Math.Min(count, scratch.Length);
                if (!ReadExactly(pipe, scratch, take)) return false;
                count -= take;
            }

            return true;
        }
    }
}
#endif
