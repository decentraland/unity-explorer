using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using ECS;
using Microsoft.ClearScript.JavaScript;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SceneRuntime.Apis.Modules.Runtime
{
    /// <summary>
    ///     The contracts correspond directly to the JS-SDK-Toolchain and its transport API.
    ///     They don't have Protobuf related stuff
    /// </summary>
    public interface IRuntime : IDisposable
    {
        public UniTask<ReadFileResponse> ReadFileAsync(string fileName, CancellationToken ct);

        public UniTask<GetWorldTimeResponse> GetWorldTimeAsync(CancellationToken ct);

        public CurrentSceneEntityResponse GetSceneInformation();

        public UniTask<GetRealmResponse> GetRealmAsync(CancellationToken ct);

        [Serializable]
        public struct GetWorldTimeResponse
        {
            public float seconds;
        }

        [Serializable]
        public struct ReadFileResponse
        {
            public ITypedArray<byte> content;
            public string hash;
        }

        [Serializable]
        public class GetRealmResponse
        {
            public RealmInfo? realmInfo;

            public GetRealmResponse(IRealmData? realmData) : this(
                realmData == null ? null : new RealmInfo(realmData)
            ) { }

            public GetRealmResponse(RealmInfo? realmInfo)
            {
                this.realmInfo = realmInfo;
            }
        }

        [Serializable]
        public class RealmInfo
        {
            public string baseUrl;
            public string realmName;
            public int networkId;
            public string commsAdapter;
            public bool preview;
            public string protocol;

            public RealmInfo(IRealmData realmData) : this(
                new Uri(realmData.Ipfs.CatalystBaseUrl.Value).GetLeftPart(UriPartial.Authority),
                realmData.RealmName,
                realmData.NetworkId,
                realmData.CommsAdapter,
                realmData.IsLocalSceneDevelopment,
                realmData.Protocol
            ) { }

            public RealmInfo(string baseUrl, string realmName, int networkId, string commsAdapter, bool isPreview,
                string protocol)
            {
                this.baseUrl = baseUrl;
                this.realmName = realmName;
                this.networkId = networkId;
                this.commsAdapter = commsAdapter;
                preview = isPreview;
                this.protocol = protocol;
            }
        }

        [Serializable]
        public struct CurrentSceneEntityResponse
        {
            /// <summary>
            ///     The URN of the scene running, which can be either the entityId or the full URN.
            /// </summary>
            public string urn;

            /// <summary>
            ///     A list containing the contents of the deployed entities.
            /// </summary>
            public string contentJson;

            /// <summary>
            ///     JSON serialization of the entity.metadata field.
            /// </summary>
            public string metadataJson;

            /// <summary>
            ///     The base URL used to resolve all content files.
            /// </summary>
            public string baseUrl;

            public CurrentSceneEntityResponse(string urn, List<ContentDefinition>? content, string metadataJson, string baseUrl)
            {
                this.urn = urn;
                contentJson = content is null ? "{}" : JsonConvert.SerializeObject(content);
                this.metadataJson = metadataJson;
                this.baseUrl = baseUrl;
            }
        }
    }
}
