using System;
using System.Runtime.Serialization;

namespace DCL.Multiplayer.Connections.Archipelago.LiveConnections
{
    public class ConnectionClosedException : Exception
    {
        protected ConnectionClosedException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public ConnectionClosedException(string message = "Connection closed") : base(message) { }

        public ConnectionClosedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
