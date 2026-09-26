using System;
using System.Collections.Generic;
using System.Text;
using Random = UnityEngine.Random;

namespace Utility
{
    public static class StringUtils
    {
        private const string CHARS = "ABCDEFGHIJ KLMNOPQRSTU VWXYZ 0123456789 abcdefghij klmnopqrstu vwxyz";
        private const int MAX_RANDOM_STRING_LENGTH = 250;

        private static readonly StringBuilder RANDOM_STRING_BUILDER_RESULT = new (MAX_RANDOM_STRING_LENGTH);
        private static readonly StringBuilder PLURALIZE_STRING_BUILDER = new (256);

        public static string GenerateRandomString(int length)
        {
            RANDOM_STRING_BUILDER_RESULT.Clear();

            for (var i = 0; i < length; i++)
                RANDOM_STRING_BUILDER_RESULT.Append(CHARS[Random.Range(0, CHARS.Length)]);

            return RANDOM_STRING_BUILDER_RESULT.ToString();
        }

        /// <summary>
        /// Formats a string with variable substitution and pluralization.
        /// Syntax: {varName:singular|plural}
        /// Example: Pluralize("You have {count:item|items}", "count", 5) => "You have 5 items"
        /// </summary>
        public static string Pluralize(this string template, string variableName, int value)
        {
            var pattern = $"{{{variableName}:";

            int startIndex = template.IndexOf(pattern, StringComparison.Ordinal);
            if (startIndex == -1) return template;

            int pipeIndex = template.IndexOf('|', startIndex);
            if (pipeIndex == -1) return template;

            int endIndex = template.IndexOf('}', pipeIndex);
            if (endIndex == -1) return template;

            int pluralizedStart = value == 1 ? startIndex + pattern.Length : pipeIndex + 1;
            int pluralizedLength = (value == 1 ? pipeIndex : endIndex) - pluralizedStart;

            try
            {
                PLURALIZE_STRING_BUILDER.Append(template.AsSpan(0, startIndex));
                PLURALIZE_STRING_BUILDER.Append(value);
                PLURALIZE_STRING_BUILDER.Append(' ');
                PLURALIZE_STRING_BUILDER.Append(template.AsSpan(pluralizedStart, pluralizedLength));
                PLURALIZE_STRING_BUILDER.Append(template.AsSpan(endIndex + 1));
                return PLURALIZE_STRING_BUILDER.ToString();
            }
            finally { PLURALIZE_STRING_BUILDER.Clear(); }
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
