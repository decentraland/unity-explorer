// SPDX-License-Identifier: MIT
//
// ffmpeg-driven Linux media player. Decoding runs out of process in ffmpeg
// (progressive files, HLS and DASH manifests, RTMP/RTSP and local paths
// alike); this class owns the playback state machine, the presentation clock
// and the texture the consumer samples.

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using DCL.Diagnostics;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.VideoPlayback
{
    /// <summary>
    /// Drives one <see cref="FfmpegSession"/> at a time. Seeks, rate changes and
    /// loop restarts each start a fresh session at the wanted position; the loop
    /// restart is prepared while the tail of the previous session is still
    /// draining, so the boundary costs no reconnect. Video is paced by the audio
    /// the mixer actually consumed and falls back to a wall clock when the source
    /// has no audio or nothing is draining it.
    /// </summary>
    internal sealed class LinuxMediaPlayer : IDisposable
    {
        private const int PROBE_TIMEOUT_MS = 30000;
        private const double STARVATION_GRACE_SECONDS = 0.1;
        private const double AUDIO_ABANDON_SECONDS = 1d;
        private const double POSITION_EPSILON_SECONDS = 0.02;
        private const double FRAME_DUE_TOLERANCE_SECONDS = 0.001;
        private const int DEFAULT_SAMPLE_RATE = 48000;
        private const int DEFAULT_CHANNELS = 2;

        private readonly object handoff = new ();
        private readonly Stopwatch wall = Stopwatch.StartNew();

        private string ffmpegPath = string.Empty;
        private string source = string.Empty;
        private MediaProbe? probe;
        private int sampleRate = DEFAULT_SAMPLE_RATE;
        private int channels = DEFAULT_CHANNELS;

        // The decoder emits BGRA; when the GPU takes that layout directly the
        // frames skip a swizzle and match the consumer's BGRA32 render targets.
        private TextureFormat textureFormat = TextureFormat.BGRA32;

        private volatile FfmpegSession? current;
        private FfmpegSession? nextLoop;
        private bool loopSpawnPending;
        private int generation;

        private bool opening;
        private bool playing;
        private bool paused;
        private bool finished;
        private bool seeking;
        private bool looping;
        private bool starved;
        private float rate = 1f;
        private double desiredStart;
        private volatile bool audioActive;

        private double anchorMedia;
        private double anchorWall;
        private bool clockRunning;
        private long lastAudioConsumed = -1;
        private double audioProgressWall;
        private double lastPresentedTime = double.NegativeInfinity;

        private Texture2D? texture;
        private ErrorCode lastError;

        private MediaProbe? pendingProbe;
        private int pendingProbeGeneration;
        private FfmpegSession? pendingCurrent;
        private int pendingCurrentGeneration;
        private FfmpegSession? pendingLoop;
        private int pendingLoopGeneration;
        private string pendingFailure = string.Empty;
        private int pendingFailureGeneration;

        public bool IsOpen => probe != null && lastError == ErrorCode.None;

        public Texture2D? Texture => texture;

        public bool IsPlaying => playing && !paused && !finished && lastError == ErrorCode.None;

        public bool IsPaused => paused && !finished;

        public bool IsFinished => finished;

        public bool IsSeeking => seeking && lastError == ErrorCode.None;

        public bool IsBuffering
        {
            get
            {
                if (!playing || paused || finished || seeking || lastError != ErrorCode.None) return false;
                if (opening) return true;
                if (current is not { } session) return true;
                return !session.PresentedAny || starved;
            }
        }

        public bool IsLooping
        {
            get => looping;
            set => looping = value;
        }

        public float PlaybackRate
        {
            get => rate;
            set => SetPlaybackRate(value);
        }

        public double Duration => probe?.Duration ?? 0d;

        public bool IsLive => probe is { IsLive: true };

        public ErrorCode LastError => lastError;

        public double CurrentTimeSeconds
        {
            get
            {
                if (seeking || probe is not { } info) return desiredStart;

                double now = ClockNow;
                if (now < 0d) now = 0d;
                if (info.Duration > 0d && now > info.Duration) now = info.Duration;
                return now;
            }
        }

        private double WallNow => wall.Elapsed.TotalSeconds;

        private double ClockNow => clockRunning ? anchorMedia + ((WallNow - anchorWall) * rate) : anchorMedia;

        /// <summary>
        /// Accepts the source and starts opening it in the background; returns
        /// false only for inputs that can never play (missing decoder binary,
        /// missing local file, empty path). Everything past that point surfaces
        /// through <see cref="LastError"/>.
        /// </summary>
        public bool OpenMedia(string path, bool autoPlay)
        {
            Close();

            if (ffmpegPath.Length == 0)
                ffmpegPath = FfmpegCommandLine.ResolveBinary(Application.dataPath) ?? string.Empty;

            if (ffmpegPath.Length == 0)
            {
                ReportHub.LogError(ReportCategory.MEDIA_STREAM, $"[LinuxMediaPlayer] No ffmpeg binary: set {FfmpegCommandLine.BINARY_ENV}, ship one under Plugins/x86_64, or install it on PATH.");
                lastError = ErrorCode.LoadFailed;
                return false;
            }

            if (!TryResolveSource(path, out string resolved))
            {
                ReportHub.LogError(ReportCategory.MEDIA_STREAM, $"[LinuxMediaPlayer] Unplayable source: {path}");
                lastError = ErrorCode.LoadFailed;
                return false;
            }

            AudioConfiguration audioConfiguration = AudioSettings.GetConfiguration();
            sampleRate = audioConfiguration.sampleRate > 0 ? audioConfiguration.sampleRate : DEFAULT_SAMPLE_RATE;
            channels = ChannelCount(audioConfiguration.speakerMode);
            textureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.BGRA32) ? TextureFormat.BGRA32 : TextureFormat.RGBA32;

            source = resolved;
            opening = true;
            playing = autoPlay;
            paused = !autoPlay;
            desiredStart = 0d;
            SetClock(0d);
            RunClock(false);
            UpdateAudioGate();

            int gen = ++generation;
            string binary = ffmpegPath;
            string target = resolved;
            bool rgbaFrames = textureFormat == TextureFormat.RGBA32;

            Task.Run(() =>
            {
                try
                {
                    MediaProbe result = MediaProbe.Run(binary, target, PROBE_TIMEOUT_MS);
                    double start;
                    float wantedRate;

                    lock (handoff)
                    {
                        pendingProbe = result;
                        pendingProbeGeneration = gen;
                        start = desiredStart;
                        wantedRate = rate;
                    }

                    FfmpegSession session = FfmpegSession.Spawn(binary, target, result, result.IsLive ? 0d : start, wantedRate, sampleRate, channels, rgbaFrames);
                    PublishSession(session, gen, false);
                }
                catch (MediaOpenException e) { PublishFailure(e.Message, gen); }
                catch (Exception e) { PublishFailure(e.ToString(), gen); }
            });

            return true;
        }

        public void Play()
        {
            if (finished)
            {
                finished = false;
                Seek(0d);
            }

            playing = true;
            paused = false;
            audioProgressWall = WallNow;
            UpdateAudioGate();
        }

        public void Pause()
        {
            paused = true;
            RunClock(false);
            UpdateAudioGate();
        }

        /// <summary>Pauses and rewinds to the start; the media stays open.</summary>
        public void Stop()
        {
            finished = false;
            paused = true;
            RunClock(false);
            Seek(0d);
            UpdateAudioGate();
        }

        public void Seek(double time)
        {
            if (double.IsNaN(time) || time < 0d) time = 0d;

            if (probe is not { } info)
            {
                desiredStart = time;
                if (opening) seeking = true;
                return;
            }

            if (info.IsLive) return;
            if (info.Duration > 0d && time > info.Duration) time = info.Duration;

            // A paused player already holding the wanted frame has nothing to do.
            if (current is { PresentedAny: true } && !clockRunning && !seeking && Math.Abs(ClockNow - time) < POSITION_EPSILON_SECONDS)
            {
                finished = false;
                desiredStart = time;
                return;
            }

            finished = false;
            desiredStart = time;
            seeking = true;
            starved = false;
            lastPresentedTime = time;
            SetClock(time);
            RunClock(false);
            UpdateAudioGate();
            RespawnCurrent(time);
        }

        public void Close()
        {
            generation++;
            DisposeInBackground(current);
            DisposeInBackground(nextLoop);
            current = null;
            nextLoop = null;
            loopSpawnPending = false;

            lock (handoff)
            {
                DisposeInBackground(pendingCurrent);
                DisposeInBackground(pendingLoop);
                pendingCurrent = null;
                pendingLoop = null;
                pendingProbe = null;
                pendingFailure = string.Empty;
            }

            probe = null;
            source = string.Empty;
            opening = false;
            playing = false;
            paused = false;
            finished = false;
            seeking = false;
            starved = false;
            desiredStart = 0d;
            lastAudioConsumed = -1;
            lastPresentedTime = double.NegativeInfinity;
            lastError = ErrorCode.None;
            SetClock(0d);
            RunClock(false);
            UpdateAudioGate();
        }

        public void Dispose()
        {
            Close();

            if (texture is { } owned)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(owned);
                else UnityEngine.Object.DestroyImmediate(owned);

                texture = null;
            }
        }

        /// <summary>Main-thread pump: swaps in sessions the worker prepared, presents due frames, tracks end of stream.</summary>
        public void UpdateTexture()
        {
            PumpHandoff();

            if (current is not { } session) return;

            if (session.Exited && session.ExitCode != 0 && !session.FailureReported)
            {
                session.FailureReported = true;
                Fail(session.AnyOutput ? ErrorCode.DecodeFailed : ErrorCode.LoadFailed, $"decoder exited with code {session.ExitCode}: {session.LogTail}");
                return;
            }

            if (session.FramingError.Length > 0 && !session.FailureReported)
            {
                session.FailureReported = true;
                Fail(ErrorCode.DecodeFailed, session.FramingError);
                return;
            }

            if (lastError != ErrorCode.None) return;

            bool active = playing && !paused && !finished;
            var audioAdvanced = false;

            if (session.Audio is { } ring && active && !seeking && session.PresentedAny)
            {
                long consumed = ring.ConsumedFrames;

                if (lastAudioConsumed >= 0 && consumed != lastAudioConsumed)
                {
                    audioAdvanced = true;
                    audioProgressWall = WallNow;
                    SetClock(session.StartTime + (consumed / (double)sampleRate * rate));
                }

                lastAudioConsumed = consumed;
            }

            double now = ClockNow;

            if (session.HasVideo)
                PresentVideo(session, active, ref now, audioAdvanced);
            else if (!session.PresentedAny && session.AnyOutput)
            {
                session.PresentedAny = true;
                seeking = false;
                SetClock(session.StartTime);
                audioProgressWall = WallNow;
                UpdateAudioGate();
            }

            RunClock(active && !seeking && session.PresentedAny);

            // Samples nobody drained for a while (mixer gone quiet) do not hold
            // the end of the stream back.
            bool audioAbandoned = active && WallNow - audioProgressWall > AUDIO_ABANDON_SECONDS;

            if (session.IsExhausted || (session.IsDrainedExceptAudio && audioAbandoned))
            {
                if (looping)
                {
                    if (nextLoop != null) SwitchToLoop();
                    else if (!loopSpawnPending) RequestLoopSession();
                }
                else if (!finished)
                {
                    DisposeInBackground(nextLoop);
                    nextLoop = null;
                    finished = true;
                    RunClock(false);
                    SetClock(probe is { Duration: > 0d } info ? info.Duration : now);
                    UpdateAudioGate();
                }
            }
            else if (looping && session.Exited && session.ExitCode == 0 && nextLoop == null && !loopSpawnPending)
                RequestLoopSession();
        }

        /// <summary>Audio-thread entry: drains the live session's ring, or silence while nothing should be heard.</summary>
        public void ReadAudioSamples(float[] dst, int dstChannels)
        {
            if (!audioActive || current is not { Audio: { } ring })
            {
                Array.Clear(dst, 0, dst.Length);
                return;
            }

            ring.Read(dst, dstChannels);
        }

        private void PresentVideo(FfmpegSession session, bool active, ref double now, bool audioAdvanced)
        {
            if (!session.PresentedAny)
            {
                if (session.TakeNext() is { } first)
                {
                    session.PresentedAny = true;
                    seeking = false;
                    lastPresentedTime = session.StartTime + first.Time;
                    SetClock(lastPresentedTime);
                    now = lastPresentedTime;
                    lastAudioConsumed = session.Audio?.ConsumedFrames ?? -1;
                    audioProgressWall = WallNow;
                    UpdateAudioGate();
                    Upload(first);
                    session.Recycle(first);
                }
            }
            else if (active && session.TakeLatestDue(now + FRAME_DUE_TOLERANCE_SECONDS - session.StartTime) is { } due)
            {
                lastPresentedTime = session.StartTime + due.Time;
                Upload(due);
                session.Recycle(due);
            }

            starved = active && !seeking && session.PresentedAny && session.IsVideoStarved
                      && now - lastPresentedTime > Math.Max(2d * session.FrameInterval, STARVATION_GRACE_SECONDS);

            // With no audio to follow, hold the clock at the next due frame so the
            // late frame is shown the moment it lands instead of being skipped.
            if (starved && !audioAdvanced)
                SetClock(lastPresentedTime + session.FrameInterval);
        }

        private void Upload(VideoFrame frame)
        {
            if (texture is not { } target)
            {
                target = new Texture2D(frame.Width, frame.Height, textureFormat, false, false) { name = "VideoPlayback-Linux-Video", wrapMode = TextureWrapMode.Clamp };
                texture = target;
            }
            else if (target.width != frame.Width || target.height != frame.Height || target.format != textureFormat)
                target.Reinitialize(frame.Width, frame.Height, textureFormat, false);

            target.LoadRawTextureData(frame.Pixels);
            target.Apply(false, false);
        }

        private void PumpHandoff()
        {
            lock (handoff)
            {
                if (pendingProbe is { } probeResult)
                {
                    if (pendingProbeGeneration == generation) probe = probeResult;
                    pendingProbe = null;
                }

                if (pendingFailure.Length > 0)
                {
                    string failure = pendingFailure;
                    pendingFailure = string.Empty;

                    if (pendingFailureGeneration == generation)
                    {
                        loopSpawnPending = false;
                        Fail(current is { AnyOutput: true } ? ErrorCode.DecodeFailed : ErrorCode.LoadFailed, failure);
                    }
                }

                if (pendingLoop is { } arrivedLoop)
                {
                    pendingLoop = null;

                    if (pendingLoopGeneration == generation) AdoptLoop(arrivedLoop);
                    else DisposeInBackground(arrivedLoop);
                }

                if (pendingCurrent is { } arrivedCurrent)
                {
                    pendingCurrent = null;

                    if (pendingCurrentGeneration == generation) AdoptCurrent(arrivedCurrent);
                    else DisposeInBackground(arrivedCurrent);
                }
            }
        }

        private void AdoptLoop(FfmpegSession session)
        {
            loopSpawnPending = false;
            DisposeInBackground(nextLoop);
            nextLoop = session;
            session.Paused = !playing || paused;
        }

        private void AdoptCurrent(FfmpegSession session)
        {
            opening = false;
            DisposeInBackground(current);
            current = session;
            session.Paused = !playing || paused;
            lastAudioConsumed = -1;
            starved = false;
            UpdateAudioGate();

            // Honour a position or rate that changed during the spawn.
            if (probe is { IsLive: false }
                && (Math.Abs(session.StartTime - desiredStart) > POSITION_EPSILON_SECONDS || Math.Abs(session.Rate - rate) > 0.001f))
            {
                seeking = true;
                SetClock(desiredStart);
                RunClock(false);
                RespawnCurrent(desiredStart);
            }
        }

        private void SwitchToLoop()
        {
            DisposeInBackground(current);
            current = nextLoop;
            nextLoop = null;
            lastAudioConsumed = -1;
            audioProgressWall = WallNow;
            starved = false;
            desiredStart = 0d;
            SetClock(0d);
            UpdateAudioGate();
        }

        private void RequestLoopSession()
        {
            if (probe is not { } info || source.Length == 0) return;

            loopSpawnPending = true;
            int gen = generation;
            string binary = ffmpegPath;
            string target = source;
            float wantedRate = rate;
            bool rgbaFrames = textureFormat == TextureFormat.RGBA32;

            Task.Run(() =>
            {
                try
                {
                    FfmpegSession session = FfmpegSession.Spawn(binary, target, info, 0d, wantedRate, sampleRate, channels, rgbaFrames);
                    PublishSession(session, gen, true);
                }
                catch (MediaOpenException e) { PublishFailure(e.Message, gen); }
                catch (Exception e) { PublishFailure(e.ToString(), gen); }
            });
        }

        private void RespawnCurrent(double start)
        {
            if (probe is not { } info || source.Length == 0) return;

            int gen = ++generation;
            DisposeInBackground(current);
            DisposeInBackground(nextLoop);
            current = null;
            nextLoop = null;
            loopSpawnPending = false;
            lastAudioConsumed = -1;
            UpdateAudioGate();

            string binary = ffmpegPath;
            string target = source;
            float wantedRate = rate;
            bool rgbaFrames = textureFormat == TextureFormat.RGBA32;

            Task.Run(() =>
            {
                try
                {
                    FfmpegSession session = FfmpegSession.Spawn(binary, target, info, start, wantedRate, sampleRate, channels, rgbaFrames);
                    PublishSession(session, gen, false);
                }
                catch (MediaOpenException e) { PublishFailure(e.Message, gen); }
                catch (Exception e) { PublishFailure(e.ToString(), gen); }
            });
        }

        private void PublishSession(FfmpegSession session, int gen, bool isLoop)
        {
            lock (handoff)
            {
                if (isLoop)
                {
                    DisposeInBackground(pendingLoop);
                    pendingLoop = session;
                    pendingLoopGeneration = gen;
                }
                else
                {
                    DisposeInBackground(pendingCurrent);
                    pendingCurrent = session;
                    pendingCurrentGeneration = gen;
                }
            }
        }

        private void PublishFailure(string message, int gen)
        {
            lock (handoff)
            {
                pendingFailure = message.Length > 0 ? message : "unknown failure";
                pendingFailureGeneration = gen;
            }
        }

        private void Fail(ErrorCode code, string message)
        {
            lastError = code;
            playing = false;
            seeking = false;
            starved = false;
            opening = false;
            loopSpawnPending = false;
            RunClock(false);
            UpdateAudioGate();
            ReportHub.LogError(ReportCategory.MEDIA_STREAM, $"[LinuxMediaPlayer] {code} for {source}: {message}");
        }

        private void SetPlaybackRate(float wanted)
        {
            wanted = FfmpegCommandLine.ClampRate(wanted);
            if (Math.Abs(wanted - rate) < 0.001f) return;

            double position = ClockNow;
            SetClock(position);
            rate = wanted;

            if (probe is not { IsLive: false } || current == null) return;

            desiredStart = position;
            seeking = true;
            SetClock(position);
            RunClock(false);
            UpdateAudioGate();
            RespawnCurrent(position);
        }

        private void SetClock(double mediaTime)
        {
            anchorMedia = mediaTime;
            anchorWall = WallNow;
        }

        private void RunClock(bool run)
        {
            if (run == clockRunning) return;
            anchorMedia = ClockNow;
            anchorWall = WallNow;
            clockRunning = run;
        }

        private void UpdateAudioGate()
        {
            bool hold = !playing || paused;
            audioActive = playing && !paused && !finished && !seeking && lastError == ErrorCode.None && current is { PresentedAny: true };

            if (current is { } session) session.Paused = hold;
            if (nextLoop is { } queued) queued.Paused = hold;
        }

        private static void DisposeInBackground(FfmpegSession? session)
        {
            if (session == null) return;
            Task.Run(session.Dispose);
        }

        private static bool TryResolveSource(string path, out string resolved)
        {
            resolved = string.Empty;
            if (string.IsNullOrWhiteSpace(path)) return false;

            string trimmed = path.Trim();

            foreach (char c in trimmed)
                if (char.IsControl(c))
                    return false;

            if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring("file://".Length);

            if (trimmed.IndexOf("://", StringComparison.Ordinal) > 0)
            {
                resolved = trimmed;
                return true;
            }

            if (!Path.IsPathRooted(trimmed) || !File.Exists(trimmed)) return false;

            resolved = trimmed;
            return true;
        }

        private static int ChannelCount(AudioSpeakerMode mode) =>
            mode switch
            {
                AudioSpeakerMode.Mono => 1,
                AudioSpeakerMode.Quad => 4,
                AudioSpeakerMode.Surround => 5,
                AudioSpeakerMode.Mode5point1 => 6,
                AudioSpeakerMode.Mode7point1 => 8,
                _ => DEFAULT_CHANNELS,
            };
    }
}
#endif
