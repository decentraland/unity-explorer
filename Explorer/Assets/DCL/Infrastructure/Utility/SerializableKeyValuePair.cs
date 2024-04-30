using System;

namespace Utility
{
    [Serializable]
    public class SerializableKeyValuePair<TKey, TValue>
    {
        public TKey key;
        public TValue value;
    }
}
