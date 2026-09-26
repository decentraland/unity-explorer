using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache.Disk.Lock
{
    public class FilesLock
    {
        private readonly HashSet<string> lockedPaths = new ();

        public LockScope TryLock(string path, out bool success)
        {
            lock (lockedPaths)
            {
                if (lockedPaths.Add(path) == false)
                {
                    success = false;
                    return LockScope.EMPTY;
                }

                success = true;
                return new LockScope(path, this);
            }
        }

        private void Unlock(string path)
        {
            lock (lockedPaths) { lockedPaths.Remove(path); }
        }

        public readonly struct LockScope : IDisposable
        {
            public static readonly LockScope EMPTY = new (string.Empty, null);

            private readonly string path;
            private readonly FilesLock? filesLock;

            public LockScope(string path, FilesLock? filesLock)
            {
                this.path = path;
                this.filesLock = filesLock;
            }

            public void Dispose()
            {
                filesLock?.Unlock(path);
            }
        }
    }
}
