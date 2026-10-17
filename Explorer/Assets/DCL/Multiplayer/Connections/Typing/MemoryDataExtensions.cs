using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Text;

namespace DCL.Multiplayer.Connections.Typing
{
    public static class MemoryDataExtensions
    {
        public static ArraySegment<byte> DangerousArraySegment(this MemoryWrap memory) =>
            new (memory.DangerousBuffer(), 0, memory.Length);

        public static string HexReadableString(this MemoryWrap memoryWrap) =>
            HexReadableString(memoryWrap.Span());

        public static string HexReadableString(this ReadOnlySpan<byte> span)
        {
            var sb = new StringBuilder(span.Length * 2);

            foreach (byte b in span)
            {
                sb.Append(b.ToString("X2"));
                sb.Append(" ");
            }

            return sb.ToString();
        }
    }
}
