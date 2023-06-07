using System;
using UnityEngine;

namespace Ipfs
{
    public static class IpfsHelper
    {
        public static Vector2Int DecodePointer(string pointer)
        {
            int commaPosition = pointer.IndexOf(",", StringComparison.Ordinal);
            ReadOnlySpan<char> span = pointer.AsSpan();

            ReadOnlySpan<char> firstPart = span[..commaPosition];
            ReadOnlySpan<char> secondPart = span[(commaPosition + 1)..];

            return new Vector2Int(int.Parse(firstPart), int.Parse(secondPart));
        }
    }
}
