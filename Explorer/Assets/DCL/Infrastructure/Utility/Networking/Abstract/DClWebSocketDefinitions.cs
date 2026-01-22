using System;
using System.Threading;
using UnityEngine.Assertions;

namespace Utility.Networking
{
    // From Microsoft
    public enum WebSocketCloseStatus
    {
        NormalClosure = 1000,
        EndpointUnavailable = 1001,
        ProtocolError = 1002,
        InvalidMessageType = 1003,
        Empty = 1005,
        // AbnormalClosure = 1006, // 1006 is reserved and should never be used by user
        InvalidPayloadData = 1007,
        PolicyViolation = 1008,
        MessageTooBig = 1009,
        MandatoryExtension = 1010,
        InternalServerError = 1011
        // non-RFC IANA registered status codes that we allow as valid closing status
        // ServiceRestart = 1012,  // indicates that the server / service is restarting.
        // TryAgainLater = 1013,   // indicates that a temporary server condition forced blocking the client's request.
        // BadGateway = 1014       // indicates that the server acting as gateway received an invalid response
        // TLSHandshakeFailed = 1015, // 1015 is reserved and should never be used by user

        // 0 - 999 Status codes in the range 0-999 are not used.
        // 1000 - 1999 Status codes in the range 1000-1999 are reserved for definition by this protocol.
        // 2000 - 2999 Status codes in the range 2000-2999 are reserved for use by extensions.
        // 3000 - 3999 Status codes in the range 3000-3999 MAY be used by libraries and frameworks. The
        //             interpretation of these codes is undefined by this protocol. End applications MUST
        //             NOT use status codes in this range.
        // 4000 - 4999 Status codes in the range 4000-4999 MAY be used by application code. The interpretation
        //             of these codes is undefined by this protocol.
    }

    [System.Runtime.CompilerServices.TypeForwardedFrom("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public enum WebSocketError
    {
        Success = 0,
        InvalidMessageType = 1,
        Faulted = 2,
        NativeError = 3,
        NotAWebSocket = 4,
        UnsupportedVersion = 5,
        UnsupportedProtocol = 6,
        HeaderError = 7,
        ConnectionClosedPrematurely = 8,
        InvalidState = 9
    }

    public enum WebSocketState
    {
        None = 0,
        Connecting = 1,
        Open = 2,
        CloseSent = 3, // WebSocket close handshake started form local endpoint
        CloseReceived = 4, // WebSocket close message received from remote endpoint. Waiting for app to call close
        Closed = 5,
        Aborted = 6,
    }

    public enum WebSocketMessageType
    {
        Text = 0,
        Binary = 1,
        Close = 2
    }

    public class WebSocketReceiveResult
    {
        public WebSocketReceiveResult(int count, WebSocketMessageType messageType, bool endOfMessage)
            : this(count, messageType, endOfMessage, null, null)
        {
        }

        public WebSocketReceiveResult(int count,
            WebSocketMessageType messageType,
            bool endOfMessage,
            WebSocketCloseStatus? closeStatus,
            string? closeStatusDescription)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException();
            //ArgumentOutOfRangeException.ThrowIfNegative(count);

            Count = count;
            EndOfMessage = endOfMessage;
            MessageType = messageType;
            CloseStatus = closeStatus;
            CloseStatusDescription = closeStatusDescription;
        }

        public int Count { get; }
        public bool EndOfMessage { get; }
        public WebSocketMessageType MessageType { get; }
        public WebSocketCloseStatus? CloseStatus { get; }
        public string? CloseStatusDescription { get; }
    }

    public class WebSocketException : Exception
    {
//#if !UNITY_WEBGL
        private System.Net.WebSockets.WebSocketException inner;
//#endif

        public WebSocketException(System.Net.WebSockets.WebSocketException inner)
        {
            this.inner = inner;
        }

        public WebSocketException(string message, Exception ex) 
            : this(new System.Net.WebSockets.WebSocketException(message, ex))
        {}

        public WebSocketException(int closeStatus, string message) 
            : this(new System.Net.WebSockets.WebSocketException(closeStatus, message))
        {}

        public WebSocketException(string message) 
            : this(new System.Net.WebSockets.WebSocketException(message))
        {}

        public WebSocketError WebSocketErrorCode => (WebSocketError)inner.WebSocketErrorCode;

        public int ErrorCode => inner.ErrorCode;

    }
}
