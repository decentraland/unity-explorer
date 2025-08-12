using System;

namespace DCL.Web3.Authenticators
{
    public class WebSocketErrorException : Exception
    {
        public WebSocketErrorException(string message) : base(message) { }
    }
}
