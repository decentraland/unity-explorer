using DCL.Optimization;
using DCL.Optimization.PerformanceBudgeting;
using System;
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

        public void Cache(string path, ReadOnlySpan<byte> sourceCode)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(sourceCode);
        }

        public bool TryGet(string path, out string sceneCode)
        {
            string filePath = FilePath(path);

            try
            {
                sceneCode = File.ReadAllText(filePath);
                return true;
            }
            catch (FileNotFoundException)
            {
                sceneCode = "";
                return false;
            }
        }

        public void Unload(IPerformanceBudget budgetToUse)
        {
            // Nothing do to. This cache does not use any memory.
        }

        private string FilePath(string path) =>
            Path.Combine(directoryPath, path);
    }
}
