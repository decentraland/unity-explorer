using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using JetBrains.Annotations;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Threading;
using UnityEngine.Networking;
using Utility;
using Utility.Times;

namespace SceneRuntime.Apis.Modules.SignedFetch
{
    public class SignedFetchWrap : IDisposable
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
        public object SignedFetch(SignedFetchRequest request)
        {
            UniTask<FlatFetchResponse> ExecuteAsync()
            {
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

                async UniTask<UnityWebRequest> RequestAsync() =>
                    method switch
                    {
                        null => (await webController.SignedFetchPostAsync(
                            request.url,
                            request.init?.body ?? string.Empty,
                            cancellationTokenSource.Token
                        )).UnityWebRequest,
                        "post" => (await webController.PostAsync(
                            request.url,
                            GenericPostArguments.CreateJsonOrDefault(request.init?.body),
                            cancellationTokenSource.Token,
                            headersInfo: headers,
                            signInfo: signInfo
                        )).UnityWebRequest,
                        "get" => (await webController.GetAsync(
                            request.url,
                            cancellationTokenSource.Token,
                            headersInfo: headers,
                            signInfo: signInfo
                        )).UnityWebRequest,
                        "put" => (await webController.PutAsync(
                            request.url,
                            GenericPutArguments.CreateJsonOrDefault(request.init?.body),
                            cancellationTokenSource.Token,
                            headersInfo: headers,
                            signInfo: signInfo
                        )).UnityWebRequest,
                        _ => throw new Exception($"Method {method} is not suppoerted for signed fetch"),
                    };

                return FlatFetchResponse.NewAsync(RequestAsync());
            }

            ReportHub.Log(ReportCategory.JAVASCRIPT, $"Signed request received {request}");

            return ExecuteAsync().ToPromise();
        }

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
        }
    }
}
