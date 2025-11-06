using DCL.Optimization.PerformanceBudgeting;
using System;
using Unity.Collections;

namespace DCL.Optimization
{
    public interface IJsSourcesCache
    {
        public void Cache(string path, ReadOnlySpan<byte> sourceCode);

        public bool TryGet(string path, out NativeArray<byte> sourceCode, Allocator allocator);

        public void Unload(IPerformanceBudget budgetToUse);

        public class Null : IJsSourcesCache
        {
            public void Cache(string path, ReadOnlySpan<byte> sourceCode)
            {
                //ignore
            }

            public bool TryGet(string path, out NativeArray<byte> sourceCode, Allocator allocator)
            {
                sourceCode = default;
                return false;
            }

            public void Unload(IPerformanceBudget budgetToUse) { }
        }
    }
}
