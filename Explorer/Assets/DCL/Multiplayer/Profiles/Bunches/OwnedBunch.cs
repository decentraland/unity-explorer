using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.Profiles.Bunches
{
    /// <summary>
    /// Thread-safe. Takes ownership of a list, and allows to read it safely, on dispose it would be cleaned and considered as performed
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public readonly struct OwnedBunch<T> : IDisposable where T: struct
    {
        private readonly Semaphore ownership;
        private readonly List<T> list;

        public OwnedBunch(Semaphore ownership, List<T> list)
        {
            ownership.WaitOne();
            this.ownership = ownership;
            this.list = list;
        }

        /// <returns>Don't save the link for the list, can be mutated at any time!</returns>
        public IReadOnlyList<T> List() =>
            list;

        public void Dispose()
        {
            list.Clear();
            ownership.Release();
        }
    }
}
