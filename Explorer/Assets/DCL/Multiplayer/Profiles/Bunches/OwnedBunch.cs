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
        private readonly ISet<T> set;

        public OwnedBunch(Semaphore ownership, ISet<T> set)
        {
            ownership.WaitOne();
            this.ownership = ownership;
            this.set = set;
        }

        /// <returns>Don't save the link for the list, can be mutated at any time!</returns>
        public ICollection<T> Collection() =>
            set;

        public void Dispose()
        {
            set.Clear();
            ownership.Release();
        }
    }
}
