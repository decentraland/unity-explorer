using System;
using System.Collections.Generic;

namespace DCL.Ipfs
{
    [Serializable]
    public class ServerConfiguration
    {
        public List<string> scenesUrn;
        public List<string> occupiedParcels;
        public string realmName;
        public int networkId;
    }
}
