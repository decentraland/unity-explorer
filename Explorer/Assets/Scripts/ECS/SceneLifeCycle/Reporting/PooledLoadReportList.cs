using DCL.AsyncLoadReporting;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;

namespace ECS.SceneLifeCycle.Reporting
{
    public readonly struct PooledLoadReportList : IDisposable
    {
        private readonly ListObjectPool<IAsyncLoadProcessReport> pool;
        internal readonly List<IAsyncLoadProcessReport> reports;

        public PooledLoadReportList(ListObjectPool<IAsyncLoadProcessReport> pool)
        {
            this.pool = pool;
            reports = pool.Get()!;
        }

        public int Count => reports.Count;

        public IAsyncLoadProcessReport this[int index] => reports[index];

        public void Dispose()
        {
            pool.Release(reports);
        }
    }
}
