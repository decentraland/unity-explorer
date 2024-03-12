using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace Utility
{
    public static class DictionaryUtils
    {
        public static async UniTask<IEnumerable<TValue>> GetKeysWithPrefixAsync<TValue>(Dictionary<string, TValue> dictionary, string prefix, CancellationToken ct)
        {
            List<TValue> result = new List<TValue>();
            foreach (var key in dictionary.Keys)
            {
                if (key.StartsWith(prefix))
                    result.Add(dictionary[key]);
            }
            await UniTask.CompletedTask;
            return result;
        }
    }
}
