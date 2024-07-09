using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility;
using Utility.Times;

namespace SceneRuntime.Apis.Modules.SignedFetch
{
    public class SignedFetchWrap : IJsApiWrapper
    {
        private readonly IWebRequestController webController;
        private readonly ISceneData sceneData;
        private readonly IRealmData realmData;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public SignedFetchWrap(IWebRequestController webController,
            ISceneData sceneData,
            IRealmData realmData)
        {
            this.webController = webController;
            this.sceneData = sceneData;
            this.realmData = realmData;
        }

        [UsedImplicitly]
        public object Headers(SignedFetchRequest signedFetchRequest)
        {
            string jsonMetaData = signedFetchRequest.init?.body ?? string.Empty;

            return new WebRequestHeadersInfo()
                  .WithSign(jsonMetaData, DateTime.UtcNow.UnixTimeAsMilliseconds())
                  .AsMutableDictionary();
        }

        [UsedImplicitly]
        public object SignedFetch(string url, string body, string headers, string method)
        {
            Dictionary<string, string>? deserializedHeaders = JsonConvert.DeserializeObject<Dictionary<string, string>>(headers);

            return SignedFetch(new SignedFetchRequest
            {
                url = url,
                init = new FlatFetchInit
                {
                    body = body,
                    headers = deserializedHeaders ?? new Dictionary<string, string>(),
                    method = string.IsNullOrEmpty(method) ? "get" : method,
                },
            });
        }

        private object SignedFetch(SignedFetchRequest request)
        {
            ReportHub.Log(ReportCategory.JAVASCRIPT, $"Signed request received {request}");

            string? method = request.init?.method?.ToLower();
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();
            string signatureMetadata = CreateSignatureMetadata();

            var headers = new WebRequestHeadersInfo(request.init?.headers)
                          // TODO: get origin & referer from program args?
                         .Add("Origin", "https://decentraland.org")
                         .Add("Referer", "https://decentraland.org")
                         .WithSign(signatureMetadata, unixTimestamp);

            var signInfo = WebRequestSignInfo.NewFromRaw(
                signatureMetadata,
                request.url,
                unixTimestamp,
                method ?? string.Empty
            );

            async Task<UniTask<FlatFetchResponse>> CreatePromiseAsync()
            {
                await UniTask.SwitchToMainThread();

                return method switch
                       {
                           null => webController.SignedFetchPostAsync<FlatFetchResponse<GenericPostRequest>, FlatFetchResponse>(
                               request.url,
                               new FlatFetchResponse<GenericPostRequest>(),
                               signatureMetadata,
                               cancellationTokenSource.Token
                           ),
                           "post" => webController.PostAsync<FlatFetchResponse<GenericPostRequest>, FlatFetchResponse>(
                               request.url,
                               new FlatFetchResponse<GenericPostRequest>(),
                               GenericPostArguments.CreateJsonOrDefault(request.init?.body),
                               cancellationTokenSource.Token,
                               headersInfo: headers,
                               signInfo: signInfo
                           ),
                           "get" => webController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(
                               request.url,
                               new FlatFetchResponse<GenericGetRequest>(),
                               cancellationTokenSource.Token,
                               headersInfo: headers,
                               signInfo: signInfo
                           ),
                           "put" => webController.PutAsync<FlatFetchResponse<GenericPutRequest>, FlatFetchResponse>(
                               request.url,
                               new FlatFetchResponse<GenericPutRequest>(),
                               GenericPutArguments.CreateJsonOrDefault(request.init?.body),
                               cancellationTokenSource.Token,
                               headersInfo: headers,
                               signInfo: signInfo
                           ),
                           _ => throw new Exception($"Method {method} is not suppoerted for signed fetch"),
                       };
            }

            return CreatePromiseAsync().Result.ToDisconnectedPromise();
        }

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
        }

        private string CreateSignatureMetadata()
        {
            Vector2Int parcel = sceneData.SceneEntityDefinition.metadata.scene.DecodedBase;

            // TODO: convert hardcoded info into program args?
            var metadata = new SignatureMetadata
            {
                origin = "https://decentraland.org",
                sceneId = $"urn:decentraland:entity:{sceneData.SceneEntityDefinition.id!}",
                parcel = $"{parcel.x},{parcel.y}",
                tld = "org",
                network = "mainnet",
                isGuest = false,
                signer = "decentraland-kernel-scene",
                realm = new SignatureMetadata.Realm
                {
                    hostname = new Uri(realmData.Ipfs.CatalystBaseUrl.ToString()).Host,
                    protocol = realmData.Protocol,
                    serverName = realmData.RealmName,
                },
            };

            return JsonUtility.ToJson(metadata);
        }

        [Serializable]
        private struct SignatureMetadata
        {
            public string origin;
            public string sceneId;
            public string parcel;

            // TODO: deprecated? https://github.com/decentraland/unity-renderer/blob/f782a1245d8737db2eeaad1939f3fbc17749f4f1/browser-interface/packages/shared/apis/host/SignedFetch.ts#L39
            public string tld;
            public string network;
            public bool isGuest;
            public Realm realm;
            public string signer;

            [Serializable]
            public struct Realm
            {
                public string hostname;
                public string protocol;
                public string serverName;
            }
        }
    }
}
