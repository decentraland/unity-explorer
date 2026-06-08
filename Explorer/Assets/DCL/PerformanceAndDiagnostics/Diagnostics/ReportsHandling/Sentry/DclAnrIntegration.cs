// Based on AnrIntegration
// TRUST_WEBGL_THREAD_SAFETY_FLAG 
#if !UNITY_WEBGL

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
using Debug = UnityEngine.Debug;
using RichTypes;
using System.IO;
using System.IO.Compression;
using DCL.Utility;

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Utility.Multithreading;

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

            Watchdog.OnApplicationNotResponding += (_, e) =>
            {
                SentryEvent se = new SentryEvent(e);
#if UNITY_STANDALONE_WIN
                hub.CaptureEvent(se, scope =>
                {
                    string? filePath = e.DumpFilePath;
                    if (filePath != null)
                    {
                        scope.AddAttachment(filePath: filePath, AttachmentType.Default);
                    }
                });
#else
                hub.CaptureEvent(se);
#endif
            };


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
            OnQuittingCleanUpCandidate oqcuc = new (nameof(DclAnrWatchDog), StopNoWait);
            ExitUtils.RegisterCleanUpCandidate(oqcuc);
        }

        internal void StopNoWait()
        {
            this.Stop(wait: false);
        }

        internal abstract void Stop(bool wait = false);

        private static (string message, string? filePath) NewDumpAttachment()
        {
#if UNITY_STANDALONE_WIN
            Result<(string filePath, string zipPath)> dumpResult = ThreadsDumpUtility.CollectAndArchiveDumpInfoToAppDir();
            if (dumpResult.Success == false)
            {
                return ($"Dump cannot be collected: {dumpResult.ErrorMessage}", null);
            }

            return ("Dump collected", dumpResult.Value.zipPath);
#else
            return ("Dump is not available on macOS yet", null);
#endif
        }

        protected void Report()
        {
            // Don't report events while in the background.
            if (!Paused)
            {
                (string dumpMessage, string? filePath) = NewDumpAttachment();

                System.Text.StringBuilder sb = new ();
                sb.Append("DclApplication not responding for at least ");
                sb.Append(DetectionTimeoutMs);
                sb.Append(" ms. ");
                sb.Append(dumpMessage);
                sb.Append(" | ");
                MultiThreadSync.AppendOwnershipTable(sb);
                string message = sb.ToString();

                Logger?.LogInfo("Detected an DclAnr event: {0}", message);

#if UNITY_STANDALONE_WIN
                var exception = new DclApplicationNotRespondingException(message, filePath);
#else
                var exception = new DclApplicationNotRespondingException(message);
#endif

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
            var waitForSeconds = new UnityEngine.WaitForSecondsRealtime((float)SleepIntervalMs / 1000);

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

        private UnityEngine.Coroutine? _updateUiStatusCoroutine;

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
            var waitForSeconds = new UnityEngine.WaitForSecondsRealtime((float)SleepIntervalMs / 1000);
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

// Not supported on macOS yet
#if UNITY_STANDALONE_WIN

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    public static class ThreadsDumpUtility
    {
        private static string APP_PATH; // Cache, because Unity API is not available from none-main thread
        private static string STREAMING_PATH; // Cache, because Unity API is not available from none-main thread

#if UNITY_EDITOR
        static ThreadsDumpUtility()
        {
            Init();
        }
#endif

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            APP_PATH = UnityEngine.Application.persistentDataPath; // Cache, because Unity API is not available from none-main thread
            STREAMING_PATH = UnityEngine.Application.streamingAssetsPath; // Cache, because Unity API is not available from none-main thread
        }

        public static Result CollectDumpInfoFile(string targetDmpPath)
        {
            Result result = MiniDumpNative.CollectSelfMiniDump(targetDmpPath);
            if (result.Success == false)
            {
                return Result.ErrorResult($"CollectSelfMiniDump error: {result.ErrorMessage}");
            }

            Result waitResult = WaitUntilDumpReady(targetDmpPath);
            if (waitResult.Success == false)
            {
                return Result.ErrorResult($"Target file is not written: {waitResult.ErrorMessage}");
            }

            return Result.SuccessResult();
        }

        public static Result WaitUntilDumpReady(string targetDmpPath)
        {
            const int TIMEOUT_MS = 5_000;
            const int POLL_MS = 100;

            Stopwatch sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < TIMEOUT_MS)
            {
                if (File.Exists(targetDmpPath))
                {
                    try
                    {
                        using FileStream stream = new FileStream(
                                targetDmpPath,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.None
                                );

                        if (stream.Length > 0)
                            return Result.SuccessResult();
                    }
                    catch (IOException)
                    {
                        // still being written
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // transient access state
                    }
                }

                Thread.Sleep(POLL_MS);
            }

            return Result.ErrorResult(
                    $"Timed out waiting for dump file: {targetDmpPath}"
                    );
        }

        public static string NewDumpFilePath()
        {
            string fileName = System.IO.Path.GetRandomFileName();
            fileName = System.IO.Path.ChangeExtension(fileName, ".dmp");
            string filePath = System.IO.Path.Combine(APP_PATH, fileName);
            return filePath;
        }

        // Returns zip path
        public static Result<string> ArchiveIntoZip(string filePath)
        {
            bool exists = System.IO.File.Exists(filePath);

            if (exists == false)
            {
                return Result<string>.ErrorResult("Original file does not exist");
            }

            string zipPath = System.IO.Path.ChangeExtension(filePath, ".zip");
            string fileName = System.IO.Path.GetFileName(filePath);

            using ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(filePath, fileName);

            return Result<string>.SuccessResult(zipPath);
        }

        public static Result<string> CollectDumpInfoBase64()
        {
            Result<(string filePath, string zipPath)> result = CollectAndArchiveDumpInfoToAppDir();
            if (result.Success == false)
            {
                return Result<string>.ErrorResult($"Error on Dump Current: {result.ErrorMessage}");
            }

            byte[] bytes = System.IO.File.ReadAllBytes(result.Value.zipPath); // yes, it allocs but rarely called
            string base64String = System.Convert.ToBase64String(bytes);

            // clean the temp files
            System.IO.File.Delete(result.Value.filePath);
            System.IO.File.Delete(result.Value.zipPath);

            return Result<string>.SuccessResult(base64String);
        }


#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/ProcDump/Dump Current")]
        public static void DumpCurrent()
        {
            Result<(string filePath, string zipPath)> result = CollectAndArchiveDumpInfoToAppDir();
            if (result.Success == false)
            {
                Debug.LogError($"Error on Dump Current: {result.ErrorMessage}");
                return;
            }

            Debug.Log($"Successfully dumped and archive at: {result.Value.filePath}, {result.Value.zipPath}");
        }
#endif

        public static Result<(string filePath, string zipPath)> CollectAndArchiveDumpInfoToAppDir()
        {
            string filePath = NewDumpFilePath();
            Result result = CollectDumpInfoFile(filePath);
            if (result.Success == false)
            {
                return Result<(string filePath, string zipPath)>.ErrorResult($"Error on dumping: {result.ErrorMessage}");
            }

            Result<string> zipPathResult = ArchiveIntoZip(filePath);
            if (zipPathResult.Success == false)
            {
                return Result<(string filePath, string zipPath)>.ErrorResult($"Error on archiving: {zipPathResult.ErrorMessage}");
            }

            string zipPath = zipPathResult.Value;

            return Result<(string filePath, string zipPath)>.SuccessResult((filePath, zipPath));
        }

    }

    internal static class ProcessInfoNative
    {
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
                uint dwDesiredAccess,
                bool bInheritHandle,
                uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        public static ProcessHandle OpenSelf(UInt32 pid)
        {
            IntPtr handle = OpenProcess(
                    PROCESS_QUERY_INFORMATION | PROCESS_VM_READ,
                    false,
                    pid);

            if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error()); // this is a really exceptional case
            return new ProcessHandle(handle);
        }

        public static void Close(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
                CloseHandle(handle);
        }

    }

    public readonly struct ProcessHandle : IDisposable
    {
        public readonly IntPtr handle;

        public ProcessHandle(IntPtr handle)
        {
            this.handle = handle;
        }

        public void Dispose()
        {
            ProcessInfoNative.Close(handle);
        }
    }

    internal static class MiniDumpNative
    {
        // from https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ne-minidumpapiset-minidump_type
        [Flags]
        private enum MINIDUMP_TYPE : uint {
            MiniDumpNormal = 0x00000000,
            MiniDumpWithDataSegs = 0x00000001,
            MiniDumpWithFullMemory = 0x00000002,
            MiniDumpWithHandleData = 0x00000004,
            MiniDumpFilterMemory = 0x00000008,
            MiniDumpScanMemory = 0x00000010,
            MiniDumpWithUnloadedModules = 0x00000020,
            MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
            MiniDumpFilterModulePaths = 0x00000080,
            MiniDumpWithProcessThreadData = 0x00000100,
            MiniDumpWithPrivateReadWriteMemory = 0x00000200,
            MiniDumpWithoutOptionalData = 0x00000400,
            MiniDumpWithFullMemoryInfo = 0x00000800,
            MiniDumpWithThreadInfo = 0x00001000,
            MiniDumpWithCodeSegs = 0x00002000,
            MiniDumpWithoutAuxiliaryState = 0x00004000,
            MiniDumpWithFullAuxiliaryState = 0x00008000,
            MiniDumpWithPrivateWriteCopyMemory = 0x00010000,
            MiniDumpIgnoreInaccessibleMemory = 0x00020000,
            MiniDumpWithTokenInformation = 0x00040000,
            MiniDumpWithModuleHeaders = 0x00080000,
            MiniDumpFilterTriage = 0x00100000,
            MiniDumpWithAvxXStateContext = 0x00200000,
            MiniDumpWithIptTrace = 0x00400000,
            MiniDumpScanInaccessiblePartialPages = 0x00800000,
            MiniDumpFilterWriteCombinedMemory,
            MiniDumpValidTypeFlags = 0x01ffffff,
            // MiniDumpNoIgnoreInaccessibleMemory,
            // MiniDumpValidTypeFlagsEx
        }

        [DllImport("Dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(
                IntPtr hProcess,
                UInt32 processId,
                IntPtr hFile,
                MINIDUMP_TYPE dumpType,
                IntPtr exceptionParam,
                IntPtr userStreamParam,
                IntPtr callbackParam
        );

        // It's approx the -mt flag
        private const MINIDUMP_TYPE dumpType = (
                MINIDUMP_TYPE.MiniDumpNormal |
                MINIDUMP_TYPE.MiniDumpWithThreadInfo |
                MINIDUMP_TYPE.MiniDumpWithHandleData |
                MINIDUMP_TYPE.MiniDumpWithUnloadedModules
                );

        public static Result CollectSelfMiniDump(string targetDmpPath)
        {
            using FileStream targetFile = File.Open(targetDmpPath, FileMode.Create, FileAccess.Write, FileShare.None);
            IntPtr hFile = targetFile.SafeFileHandle.DangerousGetHandle();

            UInt32 pid = (UInt32) Process.GetCurrentProcess().Id; // IL2CPP safe
            using ProcessHandle hProcess = ProcessInfoNative.OpenSelf(pid);

            bool ok = MiniDumpWriteDump(
                    hProcess.handle,
                    pid,
                    hFile,
                    dumpType,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero 
                    );

            if (ok == false)
            {
                return Result.ErrorResult(new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }

            return Result.SuccessResult();
        }
    }

#endif

}

#endif
