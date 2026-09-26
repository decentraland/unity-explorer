using System;
using System.Threading;
using UnityEngine;

namespace UUAV.Tests
{
    /// <summary>
    /// Captures the player's audio output for content assertions by pumping
    /// uuav_player_read_audio from a dedicated real-time thread - the same
    /// call, cadence and thread model the production DSP path uses.
    ///
    /// Capturing through a second OnAudioFilterRead component does not work:
    /// measured on Unity 6000.4, a sibling script filter receives an
    /// independent, zeroed buffer regardless of component order (tested both
    /// above and below UUAVPlayer: ~440 callbacks of peak 0.000 while the
    /// player's own filter returned real audio). CreatePlayer therefore
    /// disables the AudioSource so the player's DSP path never runs, and this
    /// tap becomes the sole consumer: it paces the media clock (which is
    /// slaved to audio consumption) and records what a DSP would have played.
    /// The pump only engages while the player's own DSP counters stay at
    /// zero, so it can never fight a live DSP for ring data.
    /// </summary>
    public sealed class AudioTapBehaviour : MonoBehaviour
    {
        private const float DspDecisionSeconds = 0.75f;
        private const int PumpIntervalMs = 10;
        private const int MaxPumpFramesPerRead = 8192;
        private const int MaxCaptureChannels = 8;
        private const float SignalAmplitude = 0.05f;

        private readonly object captureGate = new object();

        private UUAVPlayer player = null!;
        private AudioSource audioSource = null!;
        private float enabledAtRealtime;
        private long dspCallbacks;
        private volatile bool pumpMode;
        private volatile bool modeDecided;
        private volatile bool hasObservedSignal;
        private volatile bool capturing;
        private volatile float peakAmplitude;

        private float[]? captureBuffer;
        private int captureSamplesWritten;
        private int captureChannels;
        private int captureTargetFrames;

        private Thread? pumpThread;
        private volatile bool stopRequested;

        public int SampleRate { get; private set; }

        /// <summary>True once the tap knows whether a DSP is consuming the player.</summary>
        public bool ModeDecided => modeDecided;

        /// <summary>True when the tap drives audio consumption itself because no DSP does.</summary>
        public bool PumpMode => pumpMode;

        /// <summary>A sample above the signal amplitude passed through since the last reset.</summary>
        public bool HasObservedSignal => hasObservedSignal;

        /// <summary>Number of OnAudioFilterRead callbacks the tap received; diagnostic.</summary>
        public long DspCallbackCount => Interlocked.Read(ref dspCallbacks);

        /// <summary>Loudest absolute sample the tap has seen since creation; diagnostic.</summary>
        public float PeakAmplitude => peakAmplitude;

        public bool CaptureComplete
        {
            get
            {
                lock (captureGate)
                {
                    return captureBuffer != null
                           && captureChannels > 0
                           && captureSamplesWritten >= captureTargetFrames * captureChannels;
                }
            }
        }

        public void ResetSignalObservation()
        {
            hasObservedSignal = false;
        }

        /// <summary>
        /// Starts recording the next <paramref name="seconds"/> of consumed
        /// audio. Poll <see cref="CaptureComplete"/>, then read the result
        /// with <see cref="CopyCapture"/>.
        /// </summary>
        public void BeginCapture(float seconds)
        {
            lock (captureGate)
            {
                captureTargetFrames = Mathf.CeilToInt(seconds * SampleRate);
                captureBuffer = new float[captureTargetFrames * MaxCaptureChannels];
                captureSamplesWritten = 0;
                captureChannels = 0;
            }

            capturing = true;
        }

        /// <summary>
        /// Copies out the finished capture as interleaved samples. Valid once
        /// <see cref="CaptureComplete"/> is true.
        /// </summary>
        public void CopyCapture(out float[] samples, out int sampleCount, out int channels)
        {
            lock (captureGate)
            {
                if (captureBuffer == null)
                {
                    throw new InvalidOperationException("no capture was started");
                }

                sampleCount = captureSamplesWritten;
                channels = captureChannels;
                samples = new float[sampleCount];
                Array.Copy(captureBuffer, samples, sampleCount);
            }
        }

        private void Awake()
        {
            player = GetComponent<UUAVPlayer>();
            audioSource = GetComponent<AudioSource>();

            // with audio disabled (-batchmode) the reported output rate can
            // be 0, which would stop the pump from ever consuming
            int outputSampleRate = AudioSettings.outputSampleRate;
            SampleRate = outputSampleRate > 0 ? outputSampleRate : 48000;
            enabledAtRealtime = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (modeDecided == false)
            {
                DecideMode();
            }

            if (pumpMode && pumpThread == null)
            {
                StartPumpThread();
            }
        }

        private void OnDestroy()
        {
            stopRequested = true;
            pumpThread?.Join(200);
        }

        // a filter on the same GameObject would receive callbacks whenever a
        // DSP consumes the (enabled) source, even with no media open; the
        // buffer content is useless (see class comment) but the cadence is a
        // reliable liveness signal, and capturing the zeros makes a wrongly
        // enabled DSP fail content assertions loudly instead of silently
        private void OnAudioFilterRead(float[] data, int channels)
        {
            Interlocked.Increment(ref dspCallbacks);
            ObserveSignal(data, data.Length);
            Append(data, data.Length, channels);
        }

        private void DecideMode()
        {
            if (Interlocked.Read(ref dspCallbacks) > 0)
            {
                // a DSP consumes the player; pumping too would steal ring data
                modeDecided = true;
                return;
            }

            // a disabled source can never produce DSP callbacks, so there is
            // nothing to wait for; otherwise give a live DSP a moment to show
            bool dspCanExist = audioSource.enabled;
            if (dspCanExist && Time.realtimeSinceStartup - enabledAtRealtime <= DspDecisionSeconds)
            {
                return;
            }

            // last guard against double consumption: if the player's own
            // filter ran, a DSP exists even though ours never fired - a
            // wiring fault; stay undecided so captures fail loudly
            player.CopyDspStats(out long playerRequested, out _, out _);
            if (playerRequested == 0)
            {
                pumpMode = true;
                modeDecided = true;
            }
        }

        private void StartPumpThread()
        {
            ulong playerId = player.PlayerId;
            int channels = player.NativeChannels;
            if (playerId == 0 || channels <= 0)
            {
                // native creation failed or format negotiation is pending;
                // retried next Update while channels stays 0
                return;
            }

            pumpThread = new Thread(() => PumpLoop(playerId, channels))
            {
                IsBackground = true,
                Name = "UUAV.Tests.AudioPump",
            };
            pumpThread.Start();
        }

        // real-time paced consumption, immune to main-thread frame hitches;
        // a stale playerId after uuav_player_free is a native no-op, same
        // contract the production audio thread relies on
        private void PumpLoop(ulong playerId, int channels)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            double consumedFrames = 0;
            var buffer = Array.Empty<float>();

            while (stopRequested == false)
            {
                Thread.Sleep(PumpIntervalMs);

                double targetFrames = timer.Elapsed.TotalSeconds * SampleRate;

                // a long stall must not demand seconds of audio in one burst
                consumedFrames = Math.Max(consumedFrames, targetFrames - SampleRate);

                var frames = (int)Math.Min(targetFrames - consumedFrames, MaxPumpFramesPerRead);
                if (frames <= 0)
                {
                    continue;
                }

                consumedFrames += frames;

                int samples = frames * channels;
                if (buffer.Length < samples)
                {
                    buffer = new float[samples];
                }

                // a short native read leaves the tail untouched; pre-clearing
                // makes gaps show up as genuine silence in the capture
                Array.Clear(buffer, 0, samples);
                NativeMethods.uuav_player_read_audio(playerId, buffer, frames);
                ObserveSignal(buffer, samples);
                Append(buffer, samples, channels);
            }
        }

        private void ObserveSignal(float[] data, int count)
        {
            var blockPeak = 0f;
            for (var i = 0; i < count; i++)
            {
                float amplitude = Mathf.Abs(data[i]);
                if (amplitude > blockPeak)
                {
                    blockPeak = amplitude;
                }
            }

            if (blockPeak > peakAmplitude)
            {
                peakAmplitude = blockPeak;
            }

            if (blockPeak > SignalAmplitude)
            {
                hasObservedSignal = true;
            }
        }

        private void Append(float[] data, int count, int channels)
        {
            if (capturing == false)
            {
                return;
            }

            lock (captureGate)
            {
                if (captureBuffer == null)
                {
                    return;
                }

                if (captureChannels == 0)
                {
                    captureChannels = channels;
                }

                int capacity = captureTargetFrames * captureChannels;
                int toCopy = Math.Min(count, capacity - captureSamplesWritten);
                if (toCopy <= 0)
                {
                    capturing = false;
                    return;
                }

                Array.Copy(data, 0, captureBuffer, captureSamplesWritten, toCopy);
                captureSamplesWritten += toCopy;
            }
        }
    }
}
