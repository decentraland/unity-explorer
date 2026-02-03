using System.Collections.Generic;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Ipfs
{
    /// <summary>
    ///     IIpfsRealm for Decentraland worlds (worlds-content-server).
    ///     Null-safe for about responses that omit content/lambdas/configurations.
    /// </summary>
    public class WorldIpfsRealm : IIpfsRealm
    {
        private const string WORLDS_CONTENT_URL = "https://worlds-content-server.decentraland.org/contents/";

        private readonly List<string> sceneUrns;

        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }
        public URLDomain LambdasBaseUrl { get; }
        public URLDomain EntitiesActiveEndpoint { get; }
        public URLDomain AssetBundleRegistry { get; }
        public IReadOnlyList<string> SceneUrns => sceneUrns;

        public WorldIpfsRealm(string worldName, ServerAbout? serverAbout)
        {
            CatalystBaseUrl = URLDomain.FromString($"https://worlds-content-server.decentraland.org/world/{worldName}");
            ContentBaseUrl = URLDomain.FromString(WORLDS_CONTENT_URL);
            LambdasBaseUrl = URLDomain.FromString(serverAbout?.lambdas?.publicUrl ?? "https://peer.decentraland.org/lambdas/");
            EntitiesActiveEndpoint = URLDomain.EMPTY;
            AssetBundleRegistry = URLDomain.EMPTY;
            sceneUrns = serverAbout?.configurations?.scenesUrn ?? new List<string>();
        }

        public UniTask PublishAsync<T>(EntityDefinitionGeneric<T> entity, CancellationToken ct, IReadOnlyDictionary<string, byte[]>? contentFiles = null) =>
            throw new System.NotSupportedException("Publishing is not supported for worlds.");

        public string GetFileHash(byte[] file) =>
            file.IpfsHashV1();
    }
}
