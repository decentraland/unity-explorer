using System;

namespace DCL.Ipfs
{
    [Serializable]
    public class ServerAbout
    {
        public ServerConfiguration configurations;
        public ContentEndpoint content;
        public ContentEndpoint lambdas;
        public CommsInfo? comms;
    }
}
