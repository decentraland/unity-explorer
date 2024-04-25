using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
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

        public UniTask<object> FetchAsync(string requestMethod, string url, object headers, bool hasBody, string body,
            string redirect, int timeout, IWebRequestController webController, CancellationToken ct)
        {
            ReportHub
               .WithReport(ReportCategory.GENERIC_WEB_REQUEST)
               .Log(
                    "Fetch request with: "
                    + $"request method: {requestMethod} "
                    + $"url: {url} "
                    + $"headers: {headers} "
                    + $"hasBody: {hasBody} "
                    + $"body: {body} "
                    + $"redirect: {redirect} "
                    + $"timeout: {timeout} "
                );

            return origin.FetchAsync(requestMethod, url, headers, hasBody, body, redirect, timeout, webController, ct);
        }

        public void Dispose()
        {
            origin.Dispose();
        }
    }
}
