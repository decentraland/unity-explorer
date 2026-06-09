// Based on AnrIntegration
// TRUST_WEBGL_THREAD_SAFETY_FLAG
#if !UNITY_WEBGL

using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
using Utility;

using REnum;

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
                    Watchdog = new DclAnrWatchDogMultiThreaded(options.DiagnosticLogger, _monoBehaviour);
                }
            }

            Watchdog.OnApplicationNotResponding += (_, e) =>
            {
                SentryEvent se = new SentryEvent(e);
#if UNITY_STANDALONE_WIN
                hub.CaptureEvent(se, scope =>
                {
                    foreach (Result<DumpEntry> filePath in e.DumpFilePaths)
                    {
                        if (filePath.Success)
                        {
                            scope.AddAttachment(filePath: filePath.Value.path, AttachmentType.Default);
                        }
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

        // Note: we don't sleep for the whole detection timeout or we wouldn't capture if the ANR started later.
        protected const int SleepIntervalMs = 250;
        protected readonly IDiagnosticLogger? Logger;
        protected readonly SentryMonoBehaviour MonoBehaviour;
        internal event EventHandler<DclApplicationNotRespondingException> OnApplicationNotResponding = delegate { };
        protected bool Paused { get; private set; } = false;

        internal DclAnrWatchDog(IDiagnosticLogger? logger, SentryMonoBehaviour monoBehaviour)
        {
            MonoBehaviour = monoBehaviour;
            Logger = logger;

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

        // Is never supposed to be called during the pause
        protected void Report(IReadOnlyList<Result<DumpEntry>> collectedDumpPaths)
        {
            System.Text.StringBuilder sb = new ();
            sb.Append("DclApplication not responding: ");
            
            for (int i = 0; i < collectedDumpPaths.Count; i++)
            {
                sb.Append("Report ");

                Result<DumpEntry> result = collectedDumpPaths[i];
                if (result.Success)
                {
                    DumpEntry e = result.Value;
                    sb.Append(e.tresholdMs).Append("ms - ").Append(e.path);
                }
                else
                {
                    sb.Append("Error( ").Append(result.ErrorMessage).Append(" )");
                }
                sb.Append(";");
            }
            sb.Append(" | ");

            MultiThreadSync.AppendOwnershipTable(sb);
            string message = sb.ToString();

            Logger?.LogInfo("Detected an DclAnr event: {0}", message);

#if UNITY_STANDALONE_WIN
            var exception = new DclApplicationNotRespondingException(message, collectedDumpPaths);
#else
            var exception = new DclApplicationNotRespondingException(message);
#endif

            exception.SetSentryMechanism(Mechanism, "Main thread unresponsive.", false);
            OnApplicationNotResponding?.Invoke(this, exception);
        }
    }

    public readonly struct DumpEntry
    {
        public readonly string path;
        public readonly int tresholdMs;

        public DumpEntry(
            string path,
            int tresholdMs
        )
        {
            this.path = path;
            this.tresholdMs = tresholdMs;
        }
    }

#region watchdog fsm


    [REnum]
    [REnumFieldEmpty("UIHeartBeat")]
    [REnumField(typeof(int), "WatcherHeartBeatMs")]
    [REnumField(typeof(Result<DumpEntry>), "NextDumpFileCollectedPath")]
    [REnumFieldEmpty("AppPaused")]
    public partial struct WatchDogMessage {}

    public readonly struct WatchDogCollectingState
    {
        public readonly int mainThreadIsNotRespondingForMs;
        // if list is not null thats guaranteed it has values
        public readonly List<Result<DumpEntry>>? collectedDumpPaths;
        public readonly int requestedDumpCount;

        public WatchDogCollectingState(
            int mainThreadIsNotRespondingForMs,
            List<Result<DumpEntry>>? collectedDumpPaths,
            int requestedDumpCount)
        {
            this.mainThreadIsNotRespondingForMs = mainThreadIsNotRespondingForMs;
            this.collectedDumpPaths = collectedDumpPaths;
            this.requestedDumpCount = requestedDumpCount;
        }
    }

    [REnum]
    [REnumFieldEmpty("Idle")]
    [REnumField(typeof(WatchDogCollectingState), "Collecting")]
    [REnumFieldEmpty("Reported")] // don't report the same ANR instance multiple times
    public partial struct WatchDogState {}

    public readonly struct WatchDogSendTotalReportCommand
    {
        public readonly List<Result<DumpEntry>> collectedDumpPaths;

        public WatchDogSendTotalReportCommand(List<Result<DumpEntry>> collectedDumpPaths)
        {
            this.collectedDumpPaths = collectedDumpPaths;
        }
    }

    [REnum]
    [REnumField(typeof(int), "CollectNextDumpFileForTresholdMs")]
    [REnumField(typeof(WatchDogSendTotalReportCommand), "SendTotalReport")]
    [REnumField(typeof(string), "RemoveDumpFileByPath")]
    public partial struct WatchDogCommand {}


#endregion


    internal class DclAnrWatchDogMultiThreaded : DclAnrWatchDog
    {
        private static readonly IReadOnlyList<int> TRESHOLD_TO_COLLECT_NEXT_DUMP_FILE_MS = new int[]
        {
            2500, // 2.5 s
            3750, // 3.75 s
            5000, // 5 s
        };

        private readonly CancellationTokenSource cts = new ();

        private readonly Thread _thread;
        private readonly ConcurrentQueue<WatchDogMessage> messageQueue = new ();

        internal DclAnrWatchDogMultiThreaded(IDiagnosticLogger? logger, SentryMonoBehaviour monoBehaviour)
            : base(logger, monoBehaviour)
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
            cts.SafeCancelAndDispose();
            if (wait)
            {
                _thread.Join();
            }
        }

        private IEnumerator UpdateUiStatus()
        {
            CancellationToken token = cts.Token;
            var waitForSeconds = new UnityEngine.WaitForSecondsRealtime((float)SleepIntervalMs / 1000);

            yield return waitForSeconds;
            while (token.IsCancellationRequested == false)
            {
                messageQueue.Enqueue(WatchDogMessage.UIHeartBeat());
                yield return waitForSeconds;
            }
        }

        // Pure function. Verbose, but explicit. Shows every legal transation.
        // State argument gets consumed and cannot be used after the call. Ownership is passed.
        private static (WatchDogState newState, Option<WatchDogCommand> cmd) Update(WatchDogState state, WatchDogMessage message)
        {
            static (WatchDogState newState, Option<WatchDogCommand> cmd) FlushCollectingState(WatchDogCollectingState collectingState)
            {
                // Nothing to report, just get back to normal
                if (collectingState.collectedDumpPaths == null || collectingState.collectedDumpPaths.Count == 0)
                {
                    return (WatchDogState.Idle(), Option<WatchDogCommand>.None);
                }
                // Mark as reported and fire the command
                else
                {
                    WatchDogSendTotalReportCommand inner = new WatchDogSendTotalReportCommand(collectingState.collectedDumpPaths);
                    WatchDogCommand cmd = WatchDogCommand.FromSendTotalReport(inner);
                    return (WatchDogState.Reported(), Option<WatchDogCommand>.Some(cmd));
                }
            }

            return message.Match(
                state,
                onUIHeartBeat: static s => {
                    return s.Match(
                        // Idle is normal operation, just do nothing
                        onIdle: static () => (WatchDogState.Idle(), Option<WatchDogCommand>.None),
                        // UI is alive again
                        onCollecting: static collectingState => FlushCollectingState(collectingState),
                        // Resetting after the current report to idle state and is ready to detect next ANR
                        onReported: static () => (WatchDogState.Idle(), Option<WatchDogCommand>.None)
                    );
                },
                onWatcherHeartBeatMs: static (s, ms) => {
                    return s.Match(
                        ms,
                        // begin listening for heartbeats of watcher, main thread is not reporting yet
                        onIdle: static ms => {
                            WatchDogCollectingState wdcs = new WatchDogCollectingState(ms, null, requestedDumpCount: 0);
                            return (WatchDogState.FromCollecting(wdcs), Option<WatchDogCommand>.None);
                        },
                        // if the code passes next treshold -> request new dump file
                        onCollecting: static (ms, collectingState) => {
                            int newPassedIntervalMs = collectingState.mainThreadIsNotRespondingForMs + ms;

                            Option<WatchDogCommand> newCommand = Option<WatchDogCommand>.None;
                            int currentRequested = collectingState.requestedDumpCount;

                            for (int i = currentRequested; i < TRESHOLD_TO_COLLECT_NEXT_DUMP_FILE_MS.Count; i++)
                            {
                                int currentTresholdMs = TRESHOLD_TO_COLLECT_NEXT_DUMP_FILE_MS[i];
                                if (newPassedIntervalMs >= currentTresholdMs)
                                {
                                    currentRequested++;
                                    newCommand = Option<WatchDogCommand>.Some(WatchDogCommand.FromCollectNextDumpFileForTresholdMs(currentTresholdMs));
                                    break;
                                }
                            }

                            WatchDogCollectingState newState = new WatchDogCollectingState(
                                newPassedIntervalMs,
                                collectingState.collectedDumpPaths,
                                currentRequested
                            );
                            return (WatchDogState.FromCollecting(newState), newCommand);
                        },
                        // Already reported, do nothing
                        onReported: static ms => (WatchDogState.Reported(), Option<WatchDogCommand>.None)
                    );
                },
                onAppPaused: static s => {
                    return s.Match(
                        // Continue on Idle, just do nothing
                        onIdle: static () => (WatchDogState.Idle(), Option<WatchDogCommand>.None),
                        // Apps got paused from MainThread, it means the UI is alive again
                        onCollecting: static collectingState => FlushCollectingState(collectingState),
                        // Continue on Reported, just do nothing
                        onReported: static () => (WatchDogState.Reported(), Option<WatchDogCommand>.None)
                    );
                },
                onNextDumpFileCollectedPath: static (s, dmpPathResult) => {
                    return s.Match(
                        dmpPathResult,
                        // Continue on Idle and drop the file
                        onIdle: static dmpPath => (
                            WatchDogState.Idle(),
                            dmpPath.Success
                                ? Option<WatchDogCommand>.Some(WatchDogCommand.FromRemoveDumpFileByPath(dmpPath.Value.path))
                                : Option<WatchDogCommand>.None
                        ),
                        // Add to the list and fire if filled
                        onCollecting: static (dmpPath, collectingState) => {
                            if (collectingState.collectedDumpPaths == null)
                            {
                                collectingState = new WatchDogCollectingState(
                                    collectingState.mainThreadIsNotRespondingForMs,
                                    new List<Result<DumpEntry>>(),
                                    collectingState.requestedDumpCount
                                );
                            }

                            collectingState.collectedDumpPaths.Add(dmpPath);

                            if (collectingState.collectedDumpPaths.Count >= TRESHOLD_TO_COLLECT_NEXT_DUMP_FILE_MS.Count)
                            {
                                return FlushCollectingState(collectingState);
                            }

                            // if the treshold is not reached yet then continue as is
                            return (WatchDogState.FromCollecting(collectingState), Option<WatchDogCommand>.None);
                        },
                        // Continue on Reported and drop the file
                        onReported: static dmpPath => (
                            WatchDogState.Reported(),
                            dmpPath.Success
                                ? Option<WatchDogCommand>.Some(WatchDogCommand.FromRemoveDumpFileByPath(dmpPath.Value.path))
                                : Option<WatchDogCommand>.None
                        )
                    );
                }
            );
        }

        // With side effects
        private void ProcessCommand(WatchDogCommand command)
        {
            command.Match(
                this,
                onCollectNextDumpFileForTresholdMs: static (self, forMs) => {
#if UNITY_STANDALONE_WIN
                    Result<(string filePath, string zipPath)> dumpResult = ThreadsDumpUtility.CollectAndArchiveDumpInfoToAppDir();
#else
                    // MacOS is always error
                    Result<(string filePath, string zipPath)> dumpResult = Result<(string filePath, string zipPath)>.ErrorResult("MacOS doesn't support dumps");
#endif

                    Result<DumpEntry> path = default;
                    if (dumpResult.Success)
                    {
                        string rawDumpPath = dumpResult.Value.filePath;
                        if (File.Exists(rawDumpPath))
                        {
                            File.Delete(rawDumpPath);
                        }

                        DumpEntry e = new DumpEntry(dumpResult.Value.zipPath, forMs);
                        path = Result<DumpEntry>.SuccessResult(e);
                    }
                    else
                    {
                        path = Result<DumpEntry>.ErrorResult(dumpResult.ErrorMessage);
                    }

                    WatchDogMessage msg = WatchDogMessage.FromNextDumpFileCollectedPath(path);
                    self.messageQueue.Enqueue(msg);
                },
                onSendTotalReport: static (self, totalReport) => self.Report(totalReport.collectedDumpPaths),
                onRemoveDumpFileByPath: static (self, removePath) => {
                    if (File.Exists(removePath))
                    {
                        File.Delete(removePath);
                    }
                }
            );
        }

        private void Run()
        {
            try
            {
                WatchDogState currentState = WatchDogState.Idle();

                CancellationToken token = cts.Token;

                Logger?.Log(SentryLevel.Info, "Starting an DclAnr WatchDog - check every {0} ms", null, SleepIntervalMs);

                while (token.IsCancellationRequested == false)
                {
                    Thread.Sleep(SleepIntervalMs);

                    WatchDogMessage enqueueMsg;
                    if (Paused)
                    {
                        enqueueMsg = WatchDogMessage.AppPaused();
                    }
                    else
                    {
                        enqueueMsg = WatchDogMessage.FromWatcherHeartBeatMs(SleepIntervalMs);
                    }
                    messageQueue.Enqueue(enqueueMsg);

                    while (messageQueue.TryDequeue(out WatchDogMessage msg))
                    {
                        (WatchDogState newState, Option<WatchDogCommand> cmd) = Update(currentState, msg);
                        currentState = newState;

                        if (cmd.Has)
                        {
                            ProcessCommand(cmd.Value);
                        }
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
