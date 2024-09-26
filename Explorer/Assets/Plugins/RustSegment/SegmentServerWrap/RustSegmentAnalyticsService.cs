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
        private const string EMPTY_JSON = "{}";
        private volatile string cachedUserId = string.Empty;
        private readonly Dictionary<ulong, List<MarshaledString>> afterClean = new ();
        private readonly IContextSource contextSource = new ContextSource();

        public RustSegmentAnalyticsService(string writerKey)
        {
            using var mWriterKey = new MarshaledString(writerKey);
            bool result = NativeMethods.SegmentServerInitialize(mWriterKey.Ptr, Callback);

            if (result == false)
                throw new Exception("Rust Segment initialization failed");
        }

        public void Identify(string userId, JsonObject? traits = null)
        {
            cachedUserId = userId;

            var list = ListPool<MarshaledString>.Get()!;

            var mUserId = new MarshaledString(userId);
            var mTraits = new MarshaledString(traits?.ToString() ?? EMPTY_JSON);
            var mContext = new MarshaledString(contextSource.ContextJson());

            ulong operationId = NativeMethods.SegmentServerIdentify(mUserId.Ptr, mTraits.Ptr, mContext.Ptr);

            list.Add(mUserId);
            list.Add(mTraits);
            list.Add(mContext);

            lock (afterClean) { afterClean.Add(operationId, list); }
        }

        public void Track(string eventName, JsonObject? properties = null)
        {
            var list = ListPool<MarshaledString>.Get()!;

            var mUserId = new MarshaledString(cachedUserId);
            var mEventName = new MarshaledString(eventName);
            var mProperties = new MarshaledString(properties?.ToString() ?? EMPTY_JSON);
            var mContext = new MarshaledString(contextSource.ContextJson());

            ulong operationId = NativeMethods.SegmentServerTrack(mUserId.Ptr, mEventName.Ptr, mProperties.Ptr, mContext.Ptr);

            list.Add(mUserId);
            list.Add(mEventName);
            list.Add(mProperties);
            list.Add(mContext);

            lock (afterClean) { afterClean.Add(operationId, list); }
        }

        public void AddPlugin(EventPlugin plugin)
        {
            contextSource.Register(plugin);
        }

        public void Flush()
        {
            ulong operationId = NativeMethods.SegmentServerFlush();

            lock (afterClean) { afterClean.Add(operationId, ListPool<MarshaledString>.Get()!); }
        }

        private void Callback(ulong operationId, NativeMethods.Response response)
        {
            ReportHub.Log(ReportCategory.ANALYTICS, $"Segment Operation {operationId} finished with: {response}");

            if (response is not NativeMethods.Response.Success)
                ReportHub.LogError(
                    ReportCategory.ANALYTICS,
                    $"Segment operation {operationId} failed with: {response}"
                );

            CleanMemory(operationId);
        }

        private void CleanMemory(ulong operationId)
        {
            lock (afterClean)
            {
                var list = afterClean[operationId]!;
                foreach (var item in list) item.Dispose();
                afterClean.Remove(operationId);
                ListPool<MarshaledString>.Release(list);
            }
        }
    }
}
