using DCL.AsyncLoadReporting;
using System;
using System.Collections.Generic;

namespace ECS.SceneLifeCycle.Reporting
{
    public partial interface ISceneReadinessReportQueue
    {
        public interface IReportList : IReadOnlyList<AsyncLoadProcessReport>, IDisposable { }
    }
}
