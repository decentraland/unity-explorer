using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Utility;
using Utility.Times;

namespace SceneRuntime.Apis.Modules.SignedFetch
{
    public class SignedFetchWrap : IJsApiWrapper
    {
        private readonly IWebRequestController webController;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public SignedFetchWrap(IWebRequestController webController)
        {
            this.webController = webController;
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

                var headers = new WebRequestHeadersInfo(request.init?.headers)
                   .WithSign(request.init?.body ?? string.Empty, unixTimestamp);

                var signInfo = WebRequestSignInfo.NewFromRaw(
                    request.init?.body,
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
                                   request.init?.body ?? string.Empty,
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
    }
}
