using AOT;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using Plugins.RustSegment.SegmentServerWrap.ContextSources;
using Segment.Analytics;
using Segment.Serialization;
using System;
using System.Collections.Generic;
using UnityEngine.Device;
using UnityEngine.Pool;

namespace Plugins.RustSegment.SegmentServerWrap
{
    /// <summary>
    ///     This implementation is thread-safe
    /// </summary>
    public class RustSegmentAnalyticsService : IAnalyticsService
    {
        private enum Operation
        {
            Identify,
            Track,
            Flush,
        }

        private const string EMPTY_JSON = "{}";

        private readonly string anonId;
        private volatile string? cachedUserId;

        private readonly Dictionary<ulong, (Operation, List<MarshaledString>)> afterClean = new ();
        private readonly IContextSource contextSource = new ContextSource();

        private static volatile RustSegmentAnalyticsService? current;

        private long trackId;
        private long flushId;

        public RustSegmentAnalyticsService(string writerKey)
        {
            if (string.IsNullOrWhiteSpace(writerKey))
                throw new ArgumentNullException(nameof(writerKey), "Invalid key is null or empty");

            if (current != null)
                throw new Exception("Rust Segment previous instance is not disposed");

            this.anonId = SystemInfo.deviceUniqueIdentifier!;

            using var mWriterKey = new MarshaledString(writerKey);
            bool result = NativeMethods.SegmentServerInitialize(mWriterKey.Ptr, Callback);

            if (result == false)
                throw new Exception("Rust Segment initialization failed");

            ReportHub.Log(ReportCategory.ANALYTICS, "Rust Segment initialized");
            current = this;
        }

        ~RustSegmentAnalyticsService()
        {
            current = null;
            bool result = NativeMethods.SegmentServerDispose();

            if (result == false)
                throw new Exception("Rust Segment dispose failed");
        }

        public void Identify(string? userId, JsonObject? traits = null)
        {
            lock (afterClean)
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

                afterClean.Add(operationId, (Operation.Identify, list));
            }
        }

        public void Track(string eventName, JsonObject? properties = null)
        {
            lock (afterClean)
            {
#if UNITY_EDITOR || DEBUG
                ReportIfIdentityWasNotCalled();
#endif

                var list = ListPool<MarshaledString>.Get()!;

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

                afterClean.Add(operationId, (Operation.Track, list));

                trackId++;
                ReportHub.Log(ReportCategory.ANALYTICS, $"{nameof(RustSegmentAnalyticsService)} Track scheduled operationId: {operationId} trackId: {trackId}");
            }
        }

        public void AddPlugin(EventPlugin plugin)
        {
            contextSource.Register(plugin);
        }

        public void Flush()
        {
            lock (afterClean)
            {
                ulong operationId = NativeMethods.SegmentServerFlush();
                AlertIfInvalid(operationId);
                afterClean.Add(operationId, (Operation.Flush, ListPool<MarshaledString>.Get()!));

                flushId++;
                ReportHub.Log(ReportCategory.ANALYTICS, $"{nameof(RustSegmentAnalyticsService)} Flush scheduled operationId: {operationId} flushId: {flushId}");
            }
        }

        [MonoPInvokeCallback(typeof(NativeMethods.SegmentFfiCallback))]
        private static void Callback(ulong operationId, NativeMethods.Response response)
        {
            if (current == null) return;

            lock (current.afterClean)
            {
                var type = current.afterClean[operationId].Item1;

                ReportHub.Log(ReportCategory.ANALYTICS, $"Segment Operation {operationId} {type} finished with: {response}");

                if (response is not NativeMethods.Response.Success)
                    ReportHub.LogError(
                        ReportCategory.ANALYTICS,
                        $"Segment operation {operationId} {type} failed with: {response}"
                    );

                current.CleanMemory(operationId);
            }
        }

        private void CleanMemory(ulong operationId)
        {
            var list = afterClean[operationId]!;
            foreach (var item in list.Item2) item.Dispose();
            afterClean.Remove(operationId);
            ListPool<MarshaledString>.Release(list.Item2);
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
            if (string.IsNullOrWhiteSpace(cachedUserId!) && string.IsNullOrWhiteSpace(anonId!))
                ReportHub.LogError(
                    ReportCategory.ANALYTICS,
                    $"Segment to track an event, you must call Identify first"
                );
        }
    }
}
