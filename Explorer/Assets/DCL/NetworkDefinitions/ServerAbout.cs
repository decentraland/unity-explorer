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

        /// <summary>
        ///     Clears all fields before reusing this instance with FromJsonOverwrite.
        ///     Unity's JsonUtility.FromJsonOverwrite only overwrites properties present in the JSON,
        ///     so absent fields (e.g. configurations.skybox when switching to genesis) would otherwise retain previous values.
        /// </summary>
        public void Clear()
        {
            configurations.networkId = 0;
            configurations.realmName = string.Empty;
            configurations.scenesUrn?.Clear();
            configurations.skybox = new ServerSkyboxConfig { fixedHour = -1 };

            content.publicUrl = string.Empty;
            content.healthy = false;

            lambdas.publicUrl = string.Empty;
            lambdas.healthy = false;

            comms = null;
        }
    }
}
