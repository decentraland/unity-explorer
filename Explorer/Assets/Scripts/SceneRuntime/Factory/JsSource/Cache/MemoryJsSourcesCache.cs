using DCL.Optimization;
using DCL.Optimization.PerformanceBudgeting;
using System.Collections.Generic;

namespace SceneRuntime.Factory.WebSceneSource.Cache
{
    public class MemoryJsSourcesCache : IJsSourcesCache
    {
        private readonly Dictionary<string, string> cache = new ();

        public void Cache(string path, string sourceCode)
        {
            cache[path] = sourceCode;
        }

        public int Count => cache.Count;

        public bool TryGet(string path, out string? sourceCode) =>
            cache.TryGetValue(path, out sourceCode);

        public void Unload(IPerformanceBudget budgetToUse) =>
            cache.Clear();
    }
}
