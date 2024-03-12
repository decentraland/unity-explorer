using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using Microsoft.ClearScript.JavaScript;
using Nethereum.Web3;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SceneRuntime.Apis.Modules
{
    /// <summary>
    ///     The contracts correspond directly to the JS-SDK-Toolchain and its transport API.
    ///     They don't have Protobuf related stuff
    /// </summary>
    public interface IRuntime : IDisposable
    {
        public UniTask<ReadFileResponse> ReadFileAsync(string fileName, CancellationToken ct);

        public UniTask<GetWorldTimeResponse> GetWorldTimeAsync(CancellationToken ct);

        public UniTask<CurrentSceneEntityResponse> GetSceneInformationAsync(CancellationToken ct);

        public UniTask<GetRealmResponse> GetRealmAsync(CancellationToken ct);

        public struct GetWorldTimeResponse
        {
            public float seconds;
        }

        public struct ReadFileResponse
        {
            public ITypedArray<byte> content;
            public string hash;
        }

        public struct GetRealmResponse
        {
            public RealmInfo realmInfo;
        }

        public struct RealmInfo
        {
            public string baseURL;
            public string realmName;
            public int networkId;
            public string commsAdapter;
            public bool isPreview;

        }

        public struct CurrentSceneEntityResponse
        {
            // this is either the entityId or the full URN of the scene that is running
            public string Urn;
            // contents of the deployed entities
            public List<ContentDefinition> ContentMapping;
            // JSON serialization of the entity.metadata field
            public string MetadataJson;
            // baseUrl used to resolve all content files
            public string BaseUrl;
        }
    }
}
