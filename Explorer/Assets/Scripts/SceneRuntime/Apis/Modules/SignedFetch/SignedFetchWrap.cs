using DCL.Diagnostics;
using DCL.WebRequests;
using JetBrains.Annotations;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Threading;
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
        public object SignedFetch(SignedFetchRequest request)
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

                object CreatePromise() =>
                    method switch
                    {
                        null => webController.SignedFetchPostAsync(
                            request.url,
                            new FlatFetchResponse<GenericPostRequest>(),
                            request.init?.body ?? string.Empty,
                            cancellationTokenSource.Token
                        ).ToPromise(),
                        "post" => webController.PostAsync(
                            request.url,
                            new FlatFetchResponse<GenericPostRequest>(),
                            GenericPostArguments.CreateJsonOrDefault(request.init?.body),
                            cancellationTokenSource.Token,
                            headersInfo: headers,
                            signInfo: signInfo
                        ).ToPromise(),
                        "get" => webController.GetAsync(
                            request.url,
                            new FlatFetchResponse<GenericGetRequest>(),
                            cancellationTokenSource.Token,
                            headersInfo: headers,
                            signInfo: signInfo
                        ).ToPromise(),
                        "put" => webController.PutAsync(
                            request.url,
                            new FlatFetchResponse<GenericPutRequest>(),
                            GenericPutArguments.CreateJsonOrDefault(request.init?.body),
                            cancellationTokenSource.Token,
                            headersInfo: headers,
                            signInfo: signInfo
                        ).ToPromise(),
                        _ => throw new Exception($"Method {method} is not suppoerted for signed fetch"),
                    };

                return CreatePromise();
        }

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
        }
    }
}
