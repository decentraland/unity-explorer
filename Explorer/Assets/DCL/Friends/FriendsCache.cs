using System.Collections;
using System.Collections.Generic;

namespace DCL.Friends
{
    public class FriendsCache : ICollection<string>, IReadOnlyCollection<string>
    {
        private readonly HashSet<string> friends = new ();

        public bool IsReadOnly => false;

        public int Count => friends.Count;

        public IEnumerator<string> GetEnumerator() =>
            friends.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            friends.GetEnumerator();

        public void Add(string item) =>
            friends.Add(item);

        public void Clear() =>
            friends.Clear();

        public bool Contains(string item) =>
            friends.Contains(item);

        public void CopyTo(string[] array, int arrayIndex) =>
            friends.CopyTo(array, arrayIndex);

        public bool Remove(string item) =>
            friends.Remove(item);
    }
}
