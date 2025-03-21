using DCL.Optimization;
using DCL.Optimization.PerformanceBudgeting;
using System.IO;

namespace SceneRuntime.Factory.WebSceneSource.Cache
{
    public class FileJsSourcesCache : IJsSourcesCache
    {
        private readonly string directoryPath;

        public FileJsSourcesCache(string directoryPath)
        {
            this.directoryPath = directoryPath;
        }

        public void Cache(string path, string sourceCode)
        {
            File.WriteAllText(FilePath(path), sourceCode);
        }

        public bool TryGet(string path, out string? sourceCode)
        {
            string filePath = FilePath(path);

            if (File.Exists(filePath) == false)
            {
                sourceCode = null;
                return false;
            }

            sourceCode = File.ReadAllText(filePath);
            return true;
        }

        public void Unload(IPerformanceBudget budgetToUse)
        {
            // Nothing do to. This cache does not use any memory.
        }

        private string FilePath(string path) =>
            Path.Combine(directoryPath, path);
    }
}
