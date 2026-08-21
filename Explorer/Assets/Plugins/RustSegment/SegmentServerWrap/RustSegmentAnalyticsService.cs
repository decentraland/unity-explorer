using AOT;
using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using DCL.PerformanceAndDiagnostics.Analytics;
using Newtonsoft.Json.Linq;
using Plugins.RustSegment.SegmentServerWrap.ContextSources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine.Device;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using Utility.Multithreading;

namespace Plugins.RustSegment.SegmentServerWrap
{
    /// <summary>
    ///     This implementation is thread-safe
    /// </summary>
    public class RustSegmentAnalyticsService : IAnalyticsService, IDisposable
    {
        private enum Operation
        {
            Identify,
            Track,
            Flush,
        }

        private const string EMPTY_JSON = "{}";

        private static readonly TimeSpan PUMP_DELAY = TimeSpan.FromMilliseconds(500);

        // The native send daemon re-emits the "(will retry)" error every ~200ms for as long as an
        // outage lasts; the debouncer bounds Player.log and Sentry-breadcrumb volume while keeping
        // the outage onset and every distinct stuck item visible.
        private static readonly ReportData RETRY_REPORT = new (ReportCategory.ANALYTICS,
            new ReportDebounce(new ProgressiveWindowDebouncer(
                initialWindow: TimeSpan.FromSeconds(1),
                maxWindow: TimeSpan.FromMinutes(1),
                cleanUpInterval: TimeSpan.FromMinutes(5))));

        // null while no undisposed instance exists
        private static readonly Mutex<RustSegmentAnalyticsService?> CURRENT = new (null); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG


        private readonly string anonId;
        private volatile string? cachedUserId;

        private readonly DCLConcurrentDictionary<ulong, (Operation, List<MarshaledString>)> afterClean = new ();
        private readonly IContextSource contextSource = new ContextSource();
        private readonly CancellationTokenSource cancellationTokenSource;

        // Lock for public operations, cannot be used by callbacks or private methods // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
        private readonly object publicLock = new (); 

        private long trackId;
        private long flushId;

        public RustSegmentAnalyticsService(string writerKey, string? anonId)
        {
            using Mutex<RustSegmentAnalyticsService?>.Guard instanceGuard = CURRENT.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG

            if (string.IsNullOrWhiteSpace(writerKey))
                throw new ArgumentNullException(nameof(writerKey), "Invalid key is null or empty");

            if (instanceGuard.Value != null)
                throw new Exception("Rust Segment previous instance is not disposed");

            this.anonId = anonId ?? SystemInfo.deviceUniqueIdentifier!;

            // Drop legacy version if exists
            string legacyPath = Path.Combine(Application.persistentDataPath!, "analytics_queue.sqlite3");
            if (File.Exists(legacyPath))
            {
                File.Delete(legacyPath);
            }

            string path = Path.Combine(Application.persistentDataPath!, "analytics_queue_v1.sqlite3");
            const int DEFAULT_LIMIT = 500;
            using var mQueuePath = new MarshaledString(path);
            using var mWriterKey = new MarshaledString(writerKey);
            bool result = NativeMethods.SegmentServerInitialize(mQueuePath.Ptr, DEFAULT_LIMIT, mWriterKey.Ptr, Callback, ErrorCallback);

            if (result == false)
                throw new Exception("Rust Segment initialization failed");

            this.cancellationTokenSource = new CancellationTokenSource();
            PumpJobAsync(this.cancellationTokenSource).Forget();

            ReportHub.Log(ReportCategory.ANALYTICS, "Rust Segment initialized");
            instanceGuard.Value = this;
        }

        private static async UniTaskVoid PumpJobAsync(CancellationTokenSource cts)
        {
            while (cts.IsCancellationRequested == false)
            {
                int result = NativeMethods.SegmentServerPumpNextEvent();
                if (result > 0) continue; // instantly jump to new iteration;
                await UniTask.Delay(PUMP_DELAY);
            }
        }

        // must NOT have a destructor over native. Might cause the crash issue.
        public void Dispose()
        {
            lock (publicLock)
            {
                using Mutex<RustSegmentAnalyticsService?>.Guard instanceGuard = CURRENT.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG

                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();

                instanceGuard.Value = null;
                bool result = NativeMethods.SegmentServerDispose();

                if (result == false)
                    throw new Exception("Rust Segment dispose failed");
            }
        }

        public void Identify(string? userId, JObject? traits = null)
        {
            lock (publicLock)
            {
                cachedUserId = userId;

                var list = ListPool<MarshaledString>.Get()!;

                var mUserId = new MarshaledString(cachedUserId);
                var mAnonId = new MarshaledString(anonId);
                var mTraits = new MarshaledString(traits?.ToString() ?? EMPTY_JSON);
                var mContext = new MarshaledString(contextSource.ContextJson());

                ulong operationId = NativeMethods.SegmentServerIdentify(mUserId.Ptr, mAnonId.Ptr, mTraits.Ptr, mContext.Ptr);
                AlertIfInvalid(operationId);

                list.Add(mUserId);
                list.Add(mTraits);
                list.Add(mContext);

                afterClean[operationId] = (Operation.Identify, list);
            }
        }

        public void Track(string eventName, JObject? properties = null)
        {
            lock (publicLock)
            {
#if UNITY_EDITOR || DEBUG
                ReportIfIdentityWasNotCalled();
#endif

                List<MarshaledString> list = ThreadSafeListPool<MarshaledString>.SHARED.Get();

                var mUserId = new MarshaledString(cachedUserId);
                var mAnonId = new MarshaledString(anonId);
                var mEventName = new MarshaledString(eventName);
                var mProperties = new MarshaledString(properties?.ToString() ?? EMPTY_JSON);
                var mContext = new MarshaledString(contextSource.ContextJson());

                ulong operationId = NativeMethods.SegmentServerTrack(mUserId.Ptr, mAnonId.Ptr, mEventName.Ptr, mProperties.Ptr, mContext.Ptr);
                AlertIfInvalid(operationId);

                list.Add(mUserId);
                list.Add(mEventName);
                list.Add(mProperties);
                list.Add(mContext);

                afterClean[operationId] = (Operation.Track, list);

                trackId++;
                ReportHub.Log(ReportCategory.ANALYTICS, $"{nameof(RustSegmentAnalyticsService)} Track scheduled operationId: {operationId} trackId: {trackId}");
            }
        }

        public void InstantTrackAndFlush(string eventName, JObject? properties = null)
        {
            lock (publicLock)
            {
#if UNITY_EDITOR || DEBUG
                ReportIfIdentityWasNotCalled();
#endif

                List<MarshaledString> list = ThreadSafeListPool<MarshaledString>.SHARED.Get();

                var mUserId = new MarshaledString(cachedUserId);
                var mAnonId = new MarshaledString(anonId);
                var mEventName = new MarshaledString(eventName);
                var mProperties = new MarshaledString(properties?.ToString() ?? EMPTY_JSON);
                var mContext = new MarshaledString(contextSource.ContextJson());

                ulong operationId = NativeMethods.SegmentServerInstantTrackAndFlush(mUserId.Ptr, mAnonId.Ptr, mEventName.Ptr, mProperties.Ptr, mContext.Ptr);
                AlertIfInvalid(operationId);

                list.Add(mUserId);
                list.Add(mEventName);
                list.Add(mProperties);
                list.Add(mContext);

                afterClean[operationId] = (Operation.Track, list);

                trackId++;
                ReportHub.Log(ReportCategory.ANALYTICS, $"{nameof(RustSegmentAnalyticsService)} Instant Track scheduled operationId: {operationId} trackId: {trackId}");
            }
        }

        public void AddPlugin(IAnalyticsPlugin plugin)
        {
            lock (publicLock)
            {
                contextSource.Register(plugin);
            }
        }

        public void Flush()
        {
            lock (publicLock)
            {
                ulong operationId = NativeMethods.SegmentServerFlush();
                AlertIfInvalid(operationId);

                List<MarshaledString> list = ThreadSafeListPool<MarshaledString>.SHARED.Get();
                afterClean[operationId] = (Operation.Flush, list);

                flushId++;
                ReportHub.Log(ReportCategory.ANALYTICS, $"{nameof(RustSegmentAnalyticsService)} Flush scheduled operationId: {operationId} flushId: {flushId}");
            }
        }

        [MonoPInvokeCallback(typeof(NativeMethods.SegmentFfiCallback))]
        private static void ErrorCallback(IntPtr msg)
        {
            try
            {
                string marshaled = Marshal.PtrToStringUTF8(msg) ?? "cannot parse message";

                if (IsRetriedSendLoopError(marshaled))
                    ReportHub.LogWarning(RETRY_REPORT, $"Segment error: {marshaled}");
                else
                    ReportHub.LogException(new Exception($"Segment error: {marshaled}"), ReportCategory.ANALYTICS);
            }
            catch
            {
                // Ignore to avoid possibility of double exception
            }
        }

        // The native send daemon retries a "(will retry)" send-loop failure without consuming the
        // spooled item, so no events are lost and it must not bill the Sentry error budget.
        // Every other native error is a potential loss signal and keeps exception-level reporting:
        // instant_track_and_flush drops the event on send failure (bare "Network error", never
        // spooled), a failed flush drops the already-extracted batch ("Cannot flush: ..."), and
        // the "(will drop)" send-loop branch consumes the item.
        private static bool IsRetriedSendLoopError(string message) =>
            message.Contains("Error executing send loop (will retry)");

        [MonoPInvokeCallback(typeof(NativeMethods.SegmentFfiCallback))]
        private static void Callback(ulong operationId, NativeMethods.Response response)
        {
            try
            {
                using Mutex<RustSegmentAnalyticsService?>.Guard instanceGuard = CURRENT.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG

                RustSegmentAnalyticsService? current = instanceGuard.Value;

                if (current == null) return;

                Operation type = current.afterClean[operationId].Item1;

                ReportHub.Log(ReportCategory.ANALYTICS, $"Segment Operation {operationId} {type} finished with: {response}");

                // Native report_error always pairs a failed operation with an ErrorCallback delivery
                // carrying the descriptive message — severity classification lives there.
                if (response is not NativeMethods.Response.Success)
                    ReportHub.LogWarning(ReportCategory.ANALYTICS, $"Segment operation {operationId} {type} failed with: {response}");

                current.CleanMemory(operationId);
            }
            catch
            {
                // Ignore to avoid possibility of double exception
            }
        }

        private void CleanMemory(ulong operationId)
        {
            if (afterClean.TryRemove(operationId, out var list))
            {
                foreach (var item in list.Item2) item.Dispose();
                ThreadSafeListPool<MarshaledString>.SHARED.Release(list.Item2);
            }
        }

        private static void AlertIfInvalid(ulong operationId)
        {
            if (operationId == 0)
                ReportHub.LogError(
                        ReportCategory.ANALYTICS,
                        $"Segment invalid async operation is called"
                        );
        }

        private void ReportIfIdentityWasNotCalled()
        {
            if (string.IsNullOrWhiteSpace(cachedUserId) && string.IsNullOrWhiteSpace(anonId))
                ReportHub.LogError(
                        ReportCategory.ANALYTICS,
                        $"Segment to track an event, you must call Identify first"
                        );
        }
    }
}
