using DCL.DebugUtilities;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class TimeToFirstByteAverage : RequestMetricBase
    {
        private readonly Dictionary<ITypedWebRequest, DateTime> pendingRequests = new (10);

        private double sum;
        private uint count;

        public override DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.TimeNanoseconds;

        public override void Update()
        {
            TrackFirstByteDownloaded();
        }

        public override ulong GetMetric() =>
            (ulong)(sum / count) * 1_000_000UL;

        private void TrackFirstByteDownloaded()
        {
            using PooledObject<List<ITypedWebRequest>> pooledObject = ListPool<ITypedWebRequest>.Get(out List<ITypedWebRequest>? resolved);

            foreach ((ITypedWebRequest key, DateTime startTime) in pendingRequests)
            {
                if (key.UnityWebRequest.downloadedBytes <= 0) continue;

                resolved.Add(key);
                count++;
                sum += (DateTime.Now - startTime).TotalMilliseconds;
            }

            foreach (ITypedWebRequest? key in resolved)
                pendingRequests.Remove(key);
        }

        public override void OnRequestStarted(ITypedWebRequest request, DateTime startTime)
        {
            pendingRequests.Add(request, DateTime.Now);
        }

        public override void OnRequestEnded(ITypedWebRequest request, TimeSpan duration)
        {
            pendingRequests.Remove(request);
        }
    }
}
