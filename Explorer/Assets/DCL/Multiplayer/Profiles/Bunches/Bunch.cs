using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.Bunches

{
    /// <summary>
    ///     Takes ownership of a list, and allows to read it safely, on dispose it would be cleaned and considered as performed
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public readonly struct Bunch<T> : IBunch<T> where T: struct
    {
        private readonly List<T> list;

        public Bunch(List<T> list)
        {
            this.list = list;
        }

        /// <returns>Don't save the link for the list, can be mutated at any time!</returns>
        public IReadOnlyCollection<T> Collection() =>
            list;

        public bool Available() =>
            list.Count > 0;

        public void Dispose()
        {
            list.Clear();
        }
    }
}
