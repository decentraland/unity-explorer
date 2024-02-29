using LiveKit.Internal.FFIClients.Pools.Memory;
using System;

namespace DCL.Multiplayer.Connections.Typing
{
    public static class MemoryDataExtensions
    {
        public static ArraySegment<byte> DangerousArraySegment(this MemoryWrap memory) =>
            new (memory.DangerousBuffer(), 0, memory.Length);
    }
}
