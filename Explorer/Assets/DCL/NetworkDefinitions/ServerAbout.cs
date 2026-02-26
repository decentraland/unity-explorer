using System;
using System.Collections.Generic;

namespace DCL.Ipfs
{
    [Serializable]
    public class ServerAbout
    {
        public ServerConfiguration configurations;
        public ContentEndpoint content;
        public ContentEndpoint lambdas;
        public CommsInfo? comms;

        public ServerAbout(ServerConfiguration? configurations = null, ContentEndpoint? content = null, ContentEndpoint? lambdas = null, CommsInfo? comms = null)
        {
            this.configurations = configurations ?? new ServerConfiguration { networkId = 0, realmName = string.Empty, scenesUrn = new List<string>() };
            this.content = content ?? new ContentEndpoint(string.Empty);
            this.lambdas = lambdas ?? new ContentEndpoint(string.Empty);
            this.comms = comms;
        }
    }
}
