using System.Net;
using System.Net.Http;

namespace DCL.WebRequests
{
    public static class YetAnotherHttpUtils
    {
        public static bool IsRedirected(this HttpResponseMessage response) =>
            response.StatusCode is HttpStatusCode.Moved or
                HttpStatusCode.Redirect or
                HttpStatusCode.RedirectMethod or
                HttpStatusCode.TemporaryRedirect;

        public static bool IsIrrecoverableError(this IWebRequest adapter, int attemptLeft) =>
            attemptLeft <= 0 || adapter.Response.StatusCode is WebRequestUtils.NOT_FOUND or WebRequestUtils.FORBIDDEN_ACCESS || adapter.IsAborted || adapter.IsServerError();
    }
}
