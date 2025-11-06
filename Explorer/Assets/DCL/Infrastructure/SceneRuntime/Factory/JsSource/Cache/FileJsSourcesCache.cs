using DCL.Optimization;
using DCL.Optimization.PerformanceBudgeting;
using System;
using System.IO;
using Unity.Collections;

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

        public bool TryGet(string path, out NativeArray<byte> sourceCode, Allocator allocator)
        {
            string filePath = FilePath(path);

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                    FileShare.Read);

                if (stream.Length > int.MaxValue)
                    throw new IOException($"Scene code file \"{path}\" is larger than int.MaxValue");

                sourceCode = new NativeArray<byte>((int)stream.Length, allocator);

                try { ReadReliably(stream, sourceCode); }
                catch (Exception)
                {
                    sourceCode.Dispose();
                    sourceCode = default;
                    throw;
                }
            }
            catch (FileNotFoundException)
            {
                sourceCode = default;
                return false;
            }

            return true;
        }

        public void Unload(IPerformanceBudget budgetToUse)
        {
            // Nothing do to. This cache does not use any memory.
        }

        private string FilePath(string path) =>
            Path.Combine(directoryPath, path);

        private static void ReadReliably(Stream stream, Span<byte> buffer)
        {
            while (buffer.Length > 0)
            {
                int read = stream.Read(buffer);

                if (read <= 0)
                    throw new EndOfStreamException("Read zero bytes");

                buffer = buffer.Slice(read);
            }
        }
    }
}
