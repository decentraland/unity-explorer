using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using Plugins.RustSegment.SegmentServerWrap.ContextSources;
using Segment.Analytics;
using Segment.Serialization;
using System;
using System.Collections.Generic;
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
        private volatile string cachedUserId = string.Empty;
        private readonly Dictionary<ulong, (Operation, List<MarshaledString>)> afterClean = new ();
        private readonly IContextSource contextSource = new ContextSource();
        private static volatile RustSegmentAnalyticsService? current;

        public RustSegmentAnalyticsService(string writerKey)
        {
            if (current != null)
                throw new Exception("Rust Segment previous instance is not disposed");

            using var mWriterKey = new MarshaledString(writerKey);
            bool result = NativeMethods.SegmentServerInitialize(mWriterKey.Ptr, Callback);

            if (result == false)
                throw new Exception("Rust Segment initialization failed");

            ReportHub.Log(ReportData.UNSPECIFIED, "Rust Segment initialized");
            current = this;
        }

        ~RustSegmentAnalyticsService()
        {
            current = null;
            bool result = NativeMethods.SegmentServerDispose();

            if (result == false)
                throw new Exception("Rust Segment dispose failed");
        }

        public void Identify(string userId, JsonObject? traits = null)
        {
            cachedUserId = userId;

            var list = ListPool<MarshaledString>.Get()!;

            var mUserId = new MarshaledString(userId);
            var mTraits = new MarshaledString(traits?.ToString() ?? EMPTY_JSON);
            var mContext = new MarshaledString(contextSource.ContextJson());

            ulong operationId = NativeMethods.SegmentServerIdentify(mUserId.Ptr, mTraits.Ptr, mContext.Ptr);
            AlertIfInvalid(operationId);

            list.Add(mUserId);
            list.Add(mTraits);
            list.Add(mContext);

            lock (afterClean) { afterClean.Add(operationId, (Operation.Identify, list)); }
        }

        public void Track(string eventName, JsonObject? properties = null)
        {
            var list = ListPool<MarshaledString>.Get()!;

            var mUserId = new MarshaledString(cachedUserId);
            var mEventName = new MarshaledString(eventName);
            var mProperties = new MarshaledString(properties?.ToString() ?? EMPTY_JSON);
            var mContext = new MarshaledString(contextSource.ContextJson());

            ulong operationId = NativeMethods.SegmentServerTrack(mUserId.Ptr, mEventName.Ptr, mProperties.Ptr, mContext.Ptr);
            AlertIfInvalid(operationId);

            list.Add(mUserId);
            list.Add(mEventName);
            list.Add(mProperties);
            list.Add(mContext);

            lock (afterClean) { afterClean.Add(operationId, (Operation.Track, list)); }
        }

        public void AddPlugin(EventPlugin plugin)
        {
            contextSource.Register(plugin);
        }

        public void Flush()
        {
            ulong operationId = NativeMethods.SegmentServerFlush();
            AlertIfInvalid(operationId);

            lock (afterClean) { afterClean.Add(operationId, (Operation.Flush, ListPool<MarshaledString>.Get()!)); }
        }

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

        private void AlertIfInvalid(ulong operationId)
        {
            if (operationId == 0)
                ReportHub.LogError(
                    ReportCategory.ANALYTICS,
                    $"Segment invalid async operation is called"
                );
        }
    }
}
