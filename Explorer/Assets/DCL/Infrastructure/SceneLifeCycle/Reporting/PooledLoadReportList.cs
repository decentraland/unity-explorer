using DCL.Optimization.Pools;
using DCL.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ECS.SceneLifeCycle.Reporting
{
    public readonly struct PooledLoadReportList : IDisposable
    {
        private readonly ListObjectPool<AsyncLoadProcessReport> pool;
        internal readonly List<AsyncLoadProcessReport> reports;

        public PooledLoadReportList(ListObjectPool<AsyncLoadProcessReport> pool)
        {
            this.pool = pool;
            reports = pool.Get();
        }

        public int Count => reports.Count;

        public AsyncLoadProcessReport this[int index] => reports[index];

        public void Dispose()
        {
            pool.Release(reports);
        }
    }
}
