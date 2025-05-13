using DCL.DebugUtilities;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class TimeToFirstByteAverage : IRequestMetric
    {
        private readonly Dictionary<ITypedWebRequest, DateTime> pendingRequests = new (10);

        private double sum;
        private uint count;

        public DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.TimeNanoseconds;

        public void Update()
        {
            TrackFirstByteDownloaded();
        }

        public ulong GetMetric() =>
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

        public void OnRequestStarted(ITypedWebRequest request)
        {
            pendingRequests.Add(request, DateTime.Now);
        }

        public void OnRequestEnded(ITypedWebRequest request)
        {
            pendingRequests.Remove(request);
        }
    }
}
