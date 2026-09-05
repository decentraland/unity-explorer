using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Threading;

namespace SceneRuntime.Apis.Modules.FetchApi
{
    public class LogSimpleFetchApi : ISimpleFetchApi
    {
        private readonly ISimpleFetchApi origin;

        public LogSimpleFetchApi(ISimpleFetchApi origin)
        {
            this.origin = origin;
        }

        public async UniTask<ISimpleFetchApi.Response> FetchAsync(string requestMethod, string url, object headers, bool hasBody, string body,
            string redirect, int timeout, IWebRequestController webController, CancellationToken ct, bool isLocalSceneDevelopment)
        {
            string args = $"request method: {requestMethod} "
                          + $"url: {url} "
                          + $"headers: {headers} "
                          + $"hasBody: {hasBody} "
                          + $"body: {body} "
                          + $"redirect: {redirect} "
                          + $"timeout: {timeout} ";

            ReportHub.Log(ReportCategory.GENERIC_WEB_REQUEST, $"SimpleFetchApi, Fetch request started with: {args}");

            try
            {
                ISimpleFetchApi.Response result = await origin.FetchAsync(requestMethod, url, headers, hasBody, body, redirect, timeout, webController, ct, isLocalSceneDevelopment);
                ReportHub.Log(ReportCategory.GENERIC_WEB_REQUEST, $"SimpleFetchApi, Fetch request successes with: {args}");
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException operationCancelled
                                       || operationCancelled.CancellationToken != ct)
            {
                ReportHub.LogError(ReportCategory.GENERIC_WEB_REQUEST,
                    $"SimpleFetchApi, cannot make request:  {args}");

                throw;
            }
        }

        public void Dispose()
        {
            origin.Dispose();
        }
    }
}
