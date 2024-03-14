using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using Microsoft.ClearScript.JavaScript;
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

        public CurrentSceneEntityResponse GetSceneInformation();

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
            /// <summary>
            ///     The URN of the scene running, which can be either the entityId or the full URN.
            /// </summary>
            public string urn;

            /// <summary>
            ///     A list containing the contents of the deployed entities.
            /// </summary>
            public List<ContentDefinition> contentMapping;

            /// <summary>
            ///     JSON serialization of the entity.metadata field.
            /// </summary>
            public string metadataJson;

            /// <summary>
            ///     The base URL used to resolve all content files.
            /// </summary>
            public string baseUrl;
        }
    }
}
