using System;

namespace DCL.Communities
{
    public static class CommunitiesUtility
    {
        private static ReadOnlySpan<char> formatSpan => "0.#".AsSpan();

        public static string NumberToCompactString(long number)
        {
            var charsWritten = 0;
            Span<char> destination = stackalloc char[16];

            switch (number)
            {
                case >= 1_000_000_000:
                {
                    double value = number / 1_000_000_000D;
                    if (value.TryFormat(destination, out int written, formatSpan))
                    {
                        destination[written++] = 'B';
                        charsWritten = written;
                    }

                    break;
                }
                case >= 1_000_000:
                {
                    double value = number / 1_000_000D;
                    if (value.TryFormat(destination, out int written, formatSpan))
                    {
                        destination[written++] = 'M';
                        charsWritten = written;
                    }

                    break;
                }
                case >= 1_000:
                {
                    double value = number / 1_000D;
                    if (value.TryFormat(destination, out int written, formatSpan))
                    {
                        destination[written++] = 'k';
                        charsWritten = written;
                    }

                    break;
                }
                default:
                {
                    if (number.TryFormat(destination, out int written))
                    {
                        charsWritten = written;
                    }

                    break;
                }
            }

            return new string(destination.Slice(0, charsWritten));
        }
    }
}
