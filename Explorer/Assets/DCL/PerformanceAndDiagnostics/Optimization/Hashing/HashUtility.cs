using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;
using System.Text;

namespace DCL.Optimization.Hashing
{
    public class HashUtility
    {
        private static readonly IReadOnlyDictionary<byte, string> CACHED_SYMBOLS;
        private static readonly IReadOnlyDictionary<string, byte> CACHED_BYTES;
        private static readonly ThreadSafeObjectPool<StringBuilder> STRING_BUILDER_POOL = new (
            () => new StringBuilder(),
            actionOnRelease: sb => sb.Clear()
        );

        static HashUtility()
        {
            var dictionary = new Dictionary<byte, string>();
            var reverseDictionary = new Dictionary<string, byte>();

            for (var i = 0; i <= byte.MaxValue; i++)
            {
                var b = (byte)i;
                string s = dictionary[b] = i.ToString("x2");
                reverseDictionary[s] = b;
            }

            CACHED_SYMBOLS = dictionary;
            CACHED_BYTES = reverseDictionary;
        }

        public static string ByteString(ReadOnlySpan<byte> bytes)
        {
            using var __ = STRING_BUILDER_POOL.Get(out var sb);
            foreach (byte b in bytes) sb.Append(CACHED_SYMBOLS[b]!);
            return sb.ToString();
        }

        /// <summary>
        /// Not optimised for production use, replace byte[]
        /// </summary>
        public static byte[] BytesFromString(string text)
        {
            if (text.Length % 2 != 0)
                throw new Exception("Size must be even");

            var output = new byte[text.Length / 2];

            for (int i = 0; i < output.Length; i++)
            {
                string pair = text.Substring(i * 2, 2);
                output[i] = CACHED_BYTES[pair];
            }

            return output;
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
