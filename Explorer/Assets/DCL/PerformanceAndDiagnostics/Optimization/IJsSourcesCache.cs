using DCL.Optimization.PerformanceBudgeting;
using System;
using Unity.Collections;

namespace DCL.Optimization
{
    public interface IJsSourcesCache
    {
        public void Cache(string path, ReadOnlySpan<byte> sourceCode);

        public bool TryGet(string path, out string sceneCode);

        public void Unload(IPerformanceBudget budgetToUse);

        public class Null : IJsSourcesCache
        {
            public void Cache(string path, ReadOnlySpan<byte> sourceCode)
            {
                //ignore
            }

            public bool TryGet(string path, out string sceneCode)
            {
                sceneCode = "";
                return false;
            }

            public void Unload(IPerformanceBudget budgetToUse) { }
        }
    }
}
