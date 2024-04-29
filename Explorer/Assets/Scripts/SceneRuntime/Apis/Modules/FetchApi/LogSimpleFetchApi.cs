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

        public async UniTask<object> FetchAsync(string requestMethod, string url, object headers, bool hasBody, string body,
            string redirect, int timeout, IWebRequestController webController, CancellationToken ct)
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
                object result = await origin.FetchAsync(requestMethod, url, headers, hasBody, body, redirect, timeout, webController, ct);
                ReportHub.Log(ReportCategory.GENERIC_WEB_REQUEST, $"SimpleFetchApi, Fetch request successes with: {args}");
                return result;
            }
            catch (Exception e)
            {
                var exception = new Exception($"SimpleFetchApi, cannot make request: {args}", e);
                ReportHub.LogException(exception, ReportCategory.GENERIC_WEB_REQUEST);
                throw exception;
            }
        }

        public void Dispose()
        {
            origin.Dispose();
        }
    }
}
