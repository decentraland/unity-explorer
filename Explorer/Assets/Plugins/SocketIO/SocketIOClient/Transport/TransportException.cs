﻿using System;

namespace SocketIOClient.Transport
{
    public class TransportException : Exception
    {
        public TransportException() { }

        public TransportException(string message) : base(message) { }

        public TransportException(string message, Exception innerException) : base(message, innerException) { }
    }
}
