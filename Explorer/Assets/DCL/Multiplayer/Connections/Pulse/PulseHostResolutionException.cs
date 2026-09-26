using System;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PulseHostResolutionException : Exception
    {
        public PulseHostResolutionException(string hostName)
            : base($"Cannot resolve '{hostName}' to an IPv4 address") { }

        public PulseHostResolutionException(string hostName, Exception innerException)
            : base($"Cannot resolve '{hostName}'", innerException) { }
    }
}