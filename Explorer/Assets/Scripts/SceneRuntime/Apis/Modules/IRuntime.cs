using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;
using System;
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

        public struct GetWorldTimeResponse
        {
            public float seconds;
        }

        public struct ReadFileResponse
        {
            public ITypedArray<byte> content;
            public string hash;
        }

        public UniTask<GetRealmResponse> GetRealmAsync(CancellationToken ct);

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

    }
}
