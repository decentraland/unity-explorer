using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Utility
{
    public class StringUtils
    {
        private const string CHARS = "ABCDEFGHIJ KLMNOPQRSTU VWXYZ 0123456789 abcdefghij klmnopqrstu vwxyz";
        private const int MAX_RANDOM_STRING_LENGTH = 250;

        private static readonly StringBuilder RANDOM_STRING_BUILDER_RESULT = new (MAX_RANDOM_STRING_LENGTH);

        public static string GenerateRandomString(int length)
        {
            RANDOM_STRING_BUILDER_RESULT.Clear();

            for (var i = 0; i < length; i++)
                RANDOM_STRING_BUILDER_RESULT.Append(CHARS[Random.Range(0, CHARS.Length)]);

            return RANDOM_STRING_BUILDER_RESULT.ToString();
        }

        public class StringMemoryIgnoreCaseComparer : IEqualityComparer<ReadOnlyMemory<char>>
        {
            public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y) =>
                x.Span.Equals(y.Span, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode(ReadOnlyMemory<char> obj)
            {
                // In this version of .NET there is no way to get hash code from Span or Memory

                ReadOnlySpan<char> span = obj.Span;
                var hashCode = new HashCode();

                for (var i = 0; i < span.Length; i++)
                    hashCode.Add(span[i]);

                return hashCode.ToHashCode();
            }
        }
    }
}
