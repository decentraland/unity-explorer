using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Utility
{
    public static class DictionaryUtils
    {
        private const int THROTTLE_RATE = 100;

        public static async UniTask<IEnumerable<TValue>> GetKeysWithPrefixAsync<TValue>(Dictionary<string, TValue> dictionary, string prefix, List<TValue> result, CancellationToken ct)
        {
            result.Clear();
            int iterationCount = 0;
            foreach (var key in dictionary.Keys)
            {
                iterationCount++;
                if (iterationCount % THROTTLE_RATE == 0)
                {
                    await UniTask.Yield(cancellationToken: ct);
                    iterationCount = 0;
                }
                if (key.StartsWith(prefix, true, CultureInfo.InvariantCulture))
                    result.Add(dictionary[key]);
            }
            return result;
        }

        public static async UniTask<IEnumerable<TValue>> GetKeysContainingTextAsync<TValue>(IReadOnlyDictionary<string, TValue> dictionary, string matchingText, List<TValue> result, CancellationToken ct)
        {
            string sanitizedMatchingText = matchingText.Replace(":", "");
            result.Clear();
            int iterationCount = 0;
            foreach (var key in dictionary.Keys)
            {
                iterationCount++;
                if (iterationCount % THROTTLE_RATE == 0)
                {
                    await UniTask.Yield(cancellationToken: ct);
                    iterationCount = 0;
                }
                if (key.Contains(sanitizedMatchingText, StringComparison.InvariantCultureIgnoreCase))
                    result.Add(dictionary[key]);
            }
            return result;
        }
    }
}
