using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
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
        private static readonly string[] AUTH_CHAIN_HEADER_NAMES =
        {
            // AuthLinkType.SIGNER
            "x-identity-auth-chain-0",
            // AuthLinkType.ECDSA_EPHEMERAL
            "x-identity-auth-chain-1",
            // AuthLinkType.ECDSA_SIGNED_ENTITY
            "x-identity-auth-chain-2",
            // AuthLinkType.ECDSA_EIP_1654_EPHEMERAL
            "x-identity-auth-chain-3",
            // AuthLinkType.ECDSA_EIP_1654_SIGNED_ENTITY
            "x-identity-auth-chain-4",
        };

        private readonly IWebRequestController webController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly ISceneData sceneData;
        private readonly IRealmData realmData;
        private readonly IWeb3IdentityCache identityCache;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public SignedFetchWrap(
            IWebRequestController webController,
            IDecentralandUrlsSource decentralandUrlsSource,
            ISceneData sceneData,
            IRealmData realmData,
            IWeb3IdentityCache identityCache)
        {
            this.webController = webController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.sceneData = sceneData;
            this.realmData = realmData;
            this.identityCache = identityCache;
        }

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
        }

        [UsedImplicitly]
        public object GetSignedHeaders(string url, string body, string headers, string method)
        {
            Dictionary<string, string>? deserializedHeaders = JsonConvert.DeserializeObject<Dictionary<string, string>>(headers);

            string response = GetSignedHeaders(new SignedFetchRequest
            {
                url = url,
                init = new FlatFetchInit
                {
                    body = body,
                    headers = deserializedHeaders ?? new Dictionary<string, string>(),
                    method = string.IsNullOrEmpty(method) ? "get" : method,
                },
            });

            return response;
        }

        private string GetSignedHeaders(SignedFetchRequest request)
        {
            string? method = request.init?.method?.ToLower();
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();
            string? hashBody = null;

            if (request.init != null)
                if (!string.IsNullOrEmpty(request.init.body))
                    hashBody = sha256_hash(request.init.body);

            string signatureMetadata = CreateSignatureMetadata(hashBody);

            var headers = new WebRequestHeadersInfo(request.init?.headers)
                         .WithSign(signatureMetadata, unixTimestamp)
                         .AsMutableDictionary();

            var signInfo = WebRequestSignInfo.NewFromRaw(
                signatureMetadata,
                request.url,
                unixTimestamp,
                method ?? string.Empty
            );

            using AuthChain authChain = identityCache.EnsuredIdentity().Sign(signInfo.StringToSign);
            var authChainIndex = 0;

            foreach (AuthLink link in authChain)
            {
                headers[AUTH_CHAIN_HEADER_NAMES[authChainIndex]] = link.ToJson();
                authChainIndex++;
            }

            return JsonConvert.SerializeObject(headers);
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

        private static string sha256_hash(string value) {
            StringBuilder sb = new StringBuilder();

            using (SHA256 hash = SHA256.Create()) {
                Encoding enc = Encoding.UTF8;
                byte[] result = hash.ComputeHash(enc.GetBytes(value));

                foreach (byte b in result)
                    sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        private object SignedFetch(SignedFetchRequest request)
        {
            ReportHub.Log(ReportCategory.SCENE_FETCH_REQUEST, $"Signed request received {request}");

            string? method = request.init?.method?.ToLower();
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();
            string? hashBody = null;

            if (request.init != null)
                if (!string.IsNullOrEmpty(request.init.body))
                    hashBody = sha256_hash(request.init.body);

            string signatureMetadata = CreateSignatureMetadata(hashBody);

            var headers = new WebRequestHeadersInfo(request.init?.headers)
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
                            response = await webController.SignedFetchPostAsync(
                                request.url,
                                signatureMetadata,
                                GetReportData())
                                                          .ToFlatFetchResponseAsync(cancellationTokenSource.Token);

                            break;
                        case "post":
                            response = await webController.PostAsync(
                                request.url,
                                GenericUploadArguments.CreateJsonOrDefault(request.init?.body),
                                headersInfo: headers,
                                signInfo: signInfo,
                                reportCategory: GetReportData())
                                                          .ToFlatFetchResponseAsync(cancellationTokenSource.Token);

                            break;
                        case "get":
                            response = await webController.GetAsync(
                                request.url,
                                headersInfo: headers,
                                signInfo: signInfo,
                                reportData: GetReportData())
                                                          .ToFlatFetchResponseAsync(cancellationTokenSource.Token);

                            break;
                        case "put":
                            response = await webController.PutAsync(
                                request.url,
                                GenericUploadArguments.CreateJsonOrDefault(request.init?.body),
                                headersInfo: headers,
                                signInfo: signInfo,
                                reportCategory: GetReportData())
                                                          .ToFlatFetchResponseAsync(cancellationTokenSource.Token);
                            break;
                        case "delete":
                            response = await webController.DeleteAsync(
                                request.url,
                                GenericUploadArguments.CreateJsonOrDefault(request.init?.body),
                                headersInfo: headers,
                                signInfo: signInfo,
                                reportCategory: GetReportData())
                                                          .ToFlatFetchResponseAsync(cancellationTokenSource.Token);

                            break;
                        default: throw new Exception($"Method {method} is not supported for signed fetch");
                    }

                    return response;
                }
                catch (WebRequestException e)
                {
                    if (e.ResponseHeaders.TryGetValue("Content-type", out var contentType) && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
                    {
                        var flatFetchError = JsonConvert.DeserializeObject<FlatFetchError>(e.Text);
                        return new FlatFetchResponse(false, e.ResponseCode, e.ResponseCode.ToString(), flatFetchError.error,
                            e.ResponseHeaders);
                    }
                    return new FlatFetchResponse(false, e.ResponseCode, e.ResponseCode.ToString(), e.Error,
                        e.ResponseHeaders);
                }
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

        private string CreateSignatureMetadata(string? hashPayload)
        {
            Vector2Int parcel = sceneData.SceneEntityDefinition.metadata.scene.DecodedBase;

            var metadata = new SignatureMetadata
            {
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
