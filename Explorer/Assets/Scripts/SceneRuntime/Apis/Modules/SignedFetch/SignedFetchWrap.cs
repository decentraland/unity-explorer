using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using ECS;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Security.Cryptography;
using UnityEngine;
using Utility;
using Utility.Times;

namespace SceneRuntime.Apis.Modules.SignedFetch
{
    public class SignedFetchWrap : IJsApiWrapper
    {
        private readonly IWebRequestController webController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly ISceneData sceneData;
        private readonly IRealmData realmData;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public SignedFetchWrap(
            IWebRequestController webController,
            IDecentralandUrlsSource decentralandUrlsSource,
            ISceneData sceneData,
            IRealmData realmData)
        {
            this.webController = webController;
            this.decentralandUrlsSource = decentralandUrlsSource;
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

        public static String sha256_hash(String value) {
            StringBuilder Sb = new StringBuilder();

            using (SHA256 hash = SHA256Managed.Create()) {
                Encoding enc = Encoding.UTF8;
                Byte[] result = hash.ComputeHash(enc.GetBytes(value));

                foreach (Byte b in result)
                Sb.Append(b.ToString("x2"));
            }

            return Sb.ToString();
        }

        private object SignedFetch(SignedFetchRequest request)
        {
            ReportHub.Log(ReportCategory.SCENE_FETCH_REQUEST, $"Signed request received {request}");

            string? method = request.init?.method?.ToLower();
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();
            string hashBody = request.init.body.Length > 0 ? sha256_hash(request.init.body) : null;
            string signatureMetadata = CreateSignatureMetadata(hashBody);

            string hostUrl = decentralandUrlsSource.Url(DecentralandUrl.Host);

            var headers = new WebRequestHeadersInfo(request.init?.headers)
                         .Add("Origin", hostUrl)
                         .Add("Referer", hostUrl)
                         .WithSign(signatureMetadata, unixTimestamp);

            var signInfo = WebRequestSignInfo.NewFromRaw(
                signatureMetadata,
                request.url,
                unixTimestamp,
                method ?? string.Empty
            );

            async UniTask<FlatFetchResponse> ExecuteRequestAsync()
            {
                await UniTask.SwitchToMainThread();

                try
                {
                    FlatFetchResponse response;

                    switch (method)
                    {
                        case null:
                            response = await webController.SignedFetchPostAsync<FlatFetchResponse<GenericPostRequest>, FlatFetchResponse>(
                                request.url,
                                new FlatFetchResponse<GenericPostRequest>(),
                                signatureMetadata,
                                GetReportData(),
                                cancellationTokenSource.Token);

                            break;
                        case "post":
                            response = await webController.PostAsync<FlatFetchResponse<GenericPostRequest>, FlatFetchResponse>(
                                request.url,
                                new FlatFetchResponse<GenericPostRequest>(),
                                GenericPostArguments.CreateJsonOrDefault(request.init?.body),
                                cancellationTokenSource.Token,
                                headersInfo:
                                headers,
                                signInfo: signInfo,
                                reportCategory: GetReportData());

                            break;
                        case "get":
                            response = await webController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(
                                request.url,
                                new FlatFetchResponse<GenericGetRequest>(),
                                cancellationTokenSource.Token,
                                headersInfo: headers,
                                signInfo: signInfo,
                                reportData: GetReportData());

                            break;
                        case "put":
                            response = await webController.PutAsync<FlatFetchResponse<GenericPutRequest>, FlatFetchResponse>(
                                request.url,
                                new FlatFetchResponse<GenericPutRequest>(),
                                GenericPutArguments.CreateJsonOrDefault(request.init?.body),
                                cancellationTokenSource.Token,
                                headersInfo: headers,
                                signInfo: signInfo,
                                reportCategory: GetReportData());

                            break;
                        default: throw new Exception($"Method {method} is not supported for signed fetch");
                    }

                    return response;
                }
                catch (UnityWebRequestException e) { return new FlatFetchResponse(false, e.ResponseCode, e.ResponseCode.ToString(), e.Error, e.ResponseHeaders); }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.SCENE_FETCH_REQUEST));
                    throw;
                }
            }

            return ExecuteRequestAsync().ToDisconnectedPromise();
        }

        private ReportData GetReportData() =>
            new (ReportCategory.SCENE_FETCH_REQUEST, sceneShortInfo: sceneData.SceneShortInfo);

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
        }

        private string CreateSignatureMetadata(string? hashPayload)
        {
            Vector2Int parcel = sceneData.SceneEntityDefinition.metadata.scene.DecodedBase;

            var metadata = new SignatureMetadata
            {
                origin = decentralandUrlsSource.Url(DecentralandUrl.Host),
                sceneId = sceneData.SceneEntityDefinition.id!,
                parcel = $"{parcel.x},{parcel.y}",
                tld = decentralandUrlsSource.DecentralandDomain,
                network = "mainnet",

                // TODO: support guest if required in the future
                isGuest = false,
                signer = "decentraland-kernel-scene",
                // It is used for external servers to verify that the user is currently valid for that realm
                // For example the hostname can be used to form a request to: https://{hostname}/comms/peers to check the user is currently on that parcel
                realm = new SignatureMetadata.Realm
                {
                    hostname = realmData.Hostname,
                    protocol = realmData.Protocol,
                    serverName = realmData.RealmName,
                },
                hashPayload = hashPayload
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
            public string? hashPayload;

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
