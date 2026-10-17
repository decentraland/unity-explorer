using System;
using Utility.Multithreading;

namespace Utility.Ownership
{
    public readonly struct AtomicUniqueOwnership : IDisposable
    {
        private readonly Atomic<bool> isOwned;

        public AtomicUniqueOwnership(Atomic<bool> isOwned, string error = "Someone is already owns the ownership, cannot handle 2 at the same time")
        {
            if (isOwned.Value())
                throw new InvalidOperationException(error);

            isOwned.Set(true);
            this.isOwned = isOwned;
        }

        public void Dispose()
        {
            isOwned.Set(false);
        }
    }
}
