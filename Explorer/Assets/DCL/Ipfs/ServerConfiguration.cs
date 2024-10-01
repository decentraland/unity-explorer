using System;
using System.Collections.Generic;

namespace DCL.Ipfs
{
    [Serializable]
    public class ServerConfiguration
    {
        public List<string> scenesUrn;
        public List<string> localSceneParcels;
        public string realmName;
        public int networkId;
    }
}
