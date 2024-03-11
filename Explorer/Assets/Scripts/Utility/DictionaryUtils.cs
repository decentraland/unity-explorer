using System.Collections.Generic;

namespace Utility
{
    public static class DictionaryUtils
    {
        public static IEnumerable<TValue> GetKeysWithPrefix<TValue>(Dictionary<string, TValue> dictionary, string prefix)
        {
            foreach (var key in dictionary.Keys)
            {
                if (key.StartsWith(prefix))
                    yield return dictionary[key];
            }
        }
    }
}
