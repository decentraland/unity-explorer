namespace DCL.WebRequests
{
    public static class YetAnotherHttpUtils
    {
        public static bool IsIrrecoverableError(this IWebRequest adapter, int attemptLeft) =>
            attemptLeft <= 0 || adapter.Response.StatusCode is WebRequestUtils.NOT_FOUND or WebRequestUtils.FORBIDDEN_ACCESS || adapter.IsAborted || adapter.IsServerError();
    }
}
