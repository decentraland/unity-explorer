using System;
using System.Collections.Generic;

namespace DCL.Ipfs
{
    [Serializable]
    public class ServerSkyboxConfig
    {
        // Measured in seconds of a day
        public float fixedHour = -1;
    }

    [Serializable]
    public class ServerConfiguration
    {
        public List<string> scenesUrn;
        public string realmName;
        public int networkId;
        public ServerSkyboxConfig skybox;
    }
}
