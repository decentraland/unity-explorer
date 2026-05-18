// Based on AnrIntegration

using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Sentry;
using Sentry.Extensibility;
using Sentry.Integrations;
using Sentry.Unity;
using Sentry.Unity.Integrations;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DCL.Diagnostics.Sentry
{
    internal class DclAnrIntegration : ISdkIntegration
    {
        private static readonly object Lock = new();
        private static DclAnrWatchDog? Watchdog;
        private readonly SentryMonoBehaviour _monoBehaviour;

        public DclAnrIntegration(SentryMonoBehaviour monoBehaviour)
        {
            _monoBehaviour = monoBehaviour;
        }

        public void Register(IHub hub, SentryOptions sentryOptions)
        {
            var options = (SentryUnityOptions)sentryOptions;
            lock (Lock)
            {
                if (Watchdog is null)
                {
                    // Use multithreaded version for Desktop
#if !UNITY_WEBGL
                    Watchdog = new DclAnrWatchDogMultiThreaded(options.DiagnosticLogger,
                            _monoBehaviour,
                            options.AnrTimeout);
#else
                    Watchdog = new DclAnrWatchDogSingleThreaded(options.DiagnosticLogger,
                            _monoBehaviour,
                            options.AnrTimeout);
#endif
                }
            }
            Watchdog.OnApplicationNotResponding += (_, e) => hub.CaptureException(e);
        }
    }

    internal abstract class DclAnrWatchDog
    {
        public const string Mechanism = "MainThreadWatchdog";

        protected readonly int DetectionTimeoutMs;
        // Note: we don't sleep for the whole detection timeout or we wouldn't capture if the ANR started later.
        protected readonly int SleepIntervalMs;
        protected readonly IDiagnosticLogger? Logger;
        protected readonly SentryMonoBehaviour MonoBehaviour;
        internal event EventHandler<DclApplicationNotRespondingException> OnApplicationNotResponding = delegate { };
        protected bool Paused { get; private set; } = false;

        internal DclAnrWatchDog(IDiagnosticLogger? logger, SentryMonoBehaviour monoBehaviour, TimeSpan detectionTimeout)
        {
            MonoBehaviour = monoBehaviour;
            Logger = logger;
            DetectionTimeoutMs = (int)detectionTimeout.TotalMilliseconds;
            SleepIntervalMs = Math.Max(1, DetectionTimeoutMs / 5);

            MonoBehaviour.ApplicationPausing += () => Paused = true;
            MonoBehaviour.ApplicationResuming += () => Paused = false;

            // Stop when the app is being shut down. (Orignally it used IApplication from Sentry but it's internal)
            UnityEngine.Application.quitting += () => Stop();
        }

        internal abstract void Stop(bool wait = false);

        protected void Report()
        {
            // Don't report events while in the background.
            if (!Paused)
            {
                var message = $"DclApplication not responding for at least {DetectionTimeoutMs} ms.";
                Logger?.LogInfo("Detected an DclAnr event: {0}", message);

                var exception = new DclApplicationNotRespondingException(message);
                exception.SetSentryMechanism(Mechanism, "Main thread unresponsive.", false);
                OnApplicationNotResponding?.Invoke(this, exception);
            }
        }
    }

    internal class DclAnrWatchDogMultiThreaded : DclAnrWatchDog
    {
        private int _ticksSinceUiUpdate; // how many _sleepIntervalMs have elapsed since the UI updated last time
        private bool _reported; // don't report the same ANR instance multiple times
        private bool _stop;
        private readonly Thread _thread = null!;

        internal DclAnrWatchDogMultiThreaded(IDiagnosticLogger? logger, SentryMonoBehaviour monoBehaviour, TimeSpan detectionTimeout)
            : base(logger, monoBehaviour, detectionTimeout)
            {
                _thread = new Thread(Run)
                {
                    Name = "Sentry-DclAnr-WatchDog",
                    IsBackground = true, // do not block on app shutdown
                    Priority = System.Threading.ThreadPriority.BelowNormal,
                };
                _thread.Start();

                // Update the UI status periodically by running a coroutine on the UI thread
                MonoBehaviour.StartCoroutine(UpdateUiStatus());
            }

        internal override void Stop(bool wait = false)
        {
            _stop = true;
            if (wait)
            {
                _thread.Join();
            }
        }

        private IEnumerator UpdateUiStatus()
        {
            var waitForSeconds = new WaitForSecondsRealtime((float)SleepIntervalMs / 1000);

            yield return waitForSeconds;
            while (!_stop)
            {
                Interlocked.Exchange(ref _ticksSinceUiUpdate, 0);
                _reported = false;
                yield return waitForSeconds;
            }
        }

        private void Run()
        {
            try
            {
                var reportThreshold = DetectionTimeoutMs / SleepIntervalMs;

                Logger?.Log(SentryLevel.Info,
                        "Starting an DclAnr WatchDog - detection timeout: {0} ms, check every {1} ms => report after {2} failed checks",
                        null, DetectionTimeoutMs, SleepIntervalMs, reportThreshold);

                while (!_stop)
                {
                    Interlocked.Increment(ref _ticksSinceUiUpdate);
                    Thread.Sleep(SleepIntervalMs);

                    if (Paused)
                    {
                        Interlocked.Exchange(ref _ticksSinceUiUpdate, 0);
                    }
                    else if (_ticksSinceUiUpdate >= reportThreshold && !_reported)
                    {
                        Report();
                        _reported = true;
                    }
                }
            }
            catch (ThreadAbortException e)
            {
                Logger?.Log(SentryLevel.Debug, "DclAnr watchdog thread aborted.", e);
            }
            catch (Exception e)
            {
                Logger?.Log(SentryLevel.Error, "Exception in the DclAnr watchdog.", e);
            }
        }
    }

    internal class DclAnrWatchDogSingleThreaded : DclAnrWatchDog
    {
        private readonly Stopwatch _watch = new();
        private bool _stop;

        private Coroutine? _updateUiStatusCoroutine;

        internal DclAnrWatchDogSingleThreaded(IDiagnosticLogger? logger, SentryMonoBehaviour monoBehaviour, TimeSpan detectionTimeout)
            : base(logger, monoBehaviour, detectionTimeout)
            {
                Logger?.LogInfo("Starting an DclAnr Watchdog - Detection timeout: {0} ms, check every {1} ms", DetectionTimeoutMs, SleepIntervalMs);

                // Check the UI status periodically by running a coroutine on the UI thread and checking the elapsed time
                _watch.Start();
                _updateUiStatusCoroutine = MonoBehaviour.StartCoroutine(UpdateUiStatus());

                // We're stuck on the main thread, and we're using timestamps: We have to reset the coroutine when the app
                // loses and regains focus to avoid reporting false positives.
                MonoBehaviour.ApplicationPausing += () =>
                {
                    logger?.LogDebug("Stopping DclAnr detection coroutine.");
                    _watch.Stop();

                    MonoBehaviour.StopCoroutine(_updateUiStatusCoroutine);
                    _updateUiStatusCoroutine = null;
                };
                MonoBehaviour.ApplicationResuming += () =>
                {
                    logger?.LogDebug("Restarting DclAnr detection coroutine.");

                    _watch.Restart();
                    if (_updateUiStatusCoroutine is null)
                    {
                        _updateUiStatusCoroutine = MonoBehaviour.StartCoroutine(UpdateUiStatus());
                    }
                    else
                    {
                        logger?.LogError("Attempted to restart the DclAnr detection but it was not stopped.");
                    }
                };
            }

        internal override void Stop(bool wait = false)
        {
            _stop = true;
            if (_updateUiStatusCoroutine != null)
            {
                MonoBehaviour.StopCoroutine(_updateUiStatusCoroutine);
                _updateUiStatusCoroutine = null;
            }
        }

        private IEnumerator UpdateUiStatus()
        {
            _watch.Start();
            var waitForSeconds = new WaitForSecondsRealtime((float)SleepIntervalMs / 1000);
            while (!_stop)
            {
                if (_watch.ElapsedMilliseconds >= DetectionTimeoutMs)
                {
                    Report();
                }
                _watch.Restart();
                yield return waitForSeconds;
            }
        }
    }
}
