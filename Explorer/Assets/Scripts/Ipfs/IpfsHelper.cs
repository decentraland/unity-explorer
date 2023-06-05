using System;
using UnityEngine;

namespace Ipfs
{
    public static class IpfsHelper
    {
        public static Vector2Int DecodePointer(string pointer)
        {
            var commaPosition = pointer.IndexOf(",", StringComparison.Ordinal);
            var span = pointer.AsSpan();

            var firstPart = span[0..commaPosition];
            var secondPart = span[(commaPosition+1)..];

            return new Vector2Int(int.Parse(firstPart), int.Parse(secondPart));
        }
    }
}
