using Decentraland.Kernel.Comms.V3;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Runtime.CompilerServices;

namespace DCL.Multiplayer.Connections.Messaging
{
    public static class ProtobufMessageExtensions
    {
        /// <summary>
        ///     Just to localize strange behaviour with Rider and Protobuf: not finding a suitable method but everything compiles fine
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToSpan(this IMessage message, Span<byte> span)
        {
            message.WriteTo(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteTo(this IMessage message, MemoryWrap memory)
        {
            Span<byte> span = memory.Span();
            message.WriteTo(span);
        }

        /// <summary>
        ///     Just to localize strange behaviour with Rider and Protobuf: not finding a suitable method but everything compiles fine
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ServerPacket AsMessageServerPacket(this MemoryWrap memoryWrap) =>
            ServerPacket.Parser.ParseFrom(memoryWrap.Span());
    }
}
