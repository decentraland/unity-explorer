using DCL.AsyncLoadReporting;
using DCL.Optimization.Pools;
using System.Collections;
using System.Collections.Generic;

namespace ECS.SceneLifeCycle.Reporting
{
    public class PooledLoadReportList : ISceneReadinessReportQueue.IReportList
    {
        private readonly ListObjectPool<AsyncLoadProcessReport> pool;
        internal readonly List<AsyncLoadProcessReport> reports;

        public int Count => reports.Count;

        public AsyncLoadProcessReport this[int index] => reports[index];

        public PooledLoadReportList(ListObjectPool<AsyncLoadProcessReport> pool)
        {
            this.pool = pool;
            reports = pool.Get();
        }

        public void Dispose()
        {
            pool.Release(reports);
        }

        public IEnumerator<AsyncLoadProcessReport> GetEnumerator() =>
            reports.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
