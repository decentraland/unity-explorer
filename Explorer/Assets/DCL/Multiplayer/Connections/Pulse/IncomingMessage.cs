using DCL.Diagnostics;
using Decentraland.Pulse;
using System;
using Google.Protobuf;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     The underlying proto message is returned to the pool on Dispose.
    ///     It should never be stored beyond the scope of a single handler invocation.
    /// </summary>
    public readonly struct IncomingMessage : IDisposable
    {
        private static readonly ServerMessagePool POOL = new ();

        public PeerId From { get; }
        public ServerMessage Message { get; }

        private IncomingMessage(PeerId from, ServerMessage message)
        {
            From = from;
            Message = message;
        }

        public static bool TryCreate(PeerId from, ReadOnlySpan<byte> data, out IncomingMessage message)
        {
            // Extract message case without parsing so we can get the message from the pool and merge it with new data
            // Extract the field number from the first tag byte.
            // The field numbers (1–4) map directly to ServerMessage.MessageOneofCase values.
            ServerMessage.MessageOneofCase messageCase = data.Length > 0
                ? (ServerMessage.MessageOneofCase)(data[0] >> 3)
                : ServerMessage.MessageOneofCase.None;

            message = default(IncomingMessage);

            if (messageCase == ServerMessage.MessageOneofCase.None)
                return false;

            try
            {
                ServerMessage packet = POOL.Get(messageCase);
                packet.MergeFrom(data);
                message = new IncomingMessage(from, packet);
                return true;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse failed to parse packet: {e}");
                return false;
            }
        }

        public void Dispose()
        {
            POOL.Release(Message);
        }
    }

}
