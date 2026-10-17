using DCL.Optimization.PerformanceBudgeting;

namespace DCL.Optimization
{
    public interface IJsSourcesCache
    {
        void Cache(string path, string sourceCode);

        bool TryGet(string path, out string? sourceCode);

        void Unload(IPerformanceBudget budgetToUse);

        class Null : IJsSourcesCache
        {
            public void Cache(string path, string sourceCode)
            {
                //ignore
            }

            public bool TryGet(string path, out string? sourceCode)
            {
                sourceCode = null;
                return false;
            }

            public void Unload(IPerformanceBudget budgetToUse) { }
        }
    }
}
