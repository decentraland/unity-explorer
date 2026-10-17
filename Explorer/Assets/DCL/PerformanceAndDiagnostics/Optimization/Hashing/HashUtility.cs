using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;
using System.Text;

namespace DCL.Optimization.Hashing
{
    public class HashUtility
    {
        private static readonly IReadOnlyDictionary<byte, string> CACHED_SYMBOLS;
        private static readonly ThreadSafeObjectPool<StringBuilder> STRING_BUILDER_POOL = new (
            () => new StringBuilder(),
            actionOnRelease: sb => sb.Clear()
        );

        static HashUtility()
        {
            var dictionary = new Dictionary<byte, string>();

            for (var i = 0; i <= byte.MaxValue; i++)
                dictionary[(byte)i] = i.ToString("x2");

            CACHED_SYMBOLS = dictionary;
        }

        public static string ByteString(ReadOnlySpan<byte> bytes)
        {
            using var __ = STRING_BUILDER_POOL.Get(out var sb);
            foreach (byte b in bytes) sb.Append(CACHED_SYMBOLS[b]!);
            return sb.ToString();
        }

        public static void ExecutePerByte<TCtx>(ReadOnlySpan<byte> input, TCtx ctx, Action<(string stringifiedByte, TCtx context)> actionPerByte)
        {
            foreach (byte b in input)
            {
                string s = CACHED_SYMBOLS[b];
                actionPerByte((s, ctx));
            }
        }
    }
}
