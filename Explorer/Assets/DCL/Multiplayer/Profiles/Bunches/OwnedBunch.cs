using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.Multiplayer.Profiles.Bunches
{
    /// <summary>
    ///     Thread-safe. Takes ownership of a list, and allows to read it safely, on dispose it would be cleaned and considered as performed
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public readonly struct OwnedBunch<T> : IBunch<T> where T: struct
    {
        private readonly MutexSync.Scope ownership;
        private readonly HashSet<T> set;

        public OwnedBunch(MutexSync ownership, HashSet<T> set)
        {
            this.ownership = ownership.GetScope();
            this.set = set;
        }

        /// <returns>Don't save the link for the list, can be mutated at any time!</returns>
        public IReadOnlyCollection<T> Collection() =>
            set;

        public bool Available() =>
            set.Count > 0;

        public void Dispose()
        {
            set.Clear();
            ownership.Dispose();
        }
    }
}
