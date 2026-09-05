namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     OpenTelemetry HTTP semantic conventions for client requests.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Request attributes:</b>
    ///     </para>
    ///     <list type="bullet">
    ///         <item><c>http.method</c> - HTTP method (GET, POST, PUT, DELETE)</item>
    ///         <item><c>http.url</c> - Full URL (https://example.com:8080/path?query=1)</item>
    ///         <item><c>http.scheme</c> - URI scheme (http, https)</item>
    ///         <item><c>http.host</c> - Host and port (example.com:8080)</item>
    ///         <item><c>http.target</c> - Path and query string (/path?query=1)</item>
    ///         <item><c>http.flavor</c> - HTTP version (1.0, 1.1, 2.0)</item>
    ///         <item><c>http.user_agent</c> - User-Agent header</item>
    ///         <item><c>http.request_content_length</c> - Request body size in bytes</item>
    ///         <item><c>http.request_content_length_uncompressed</c> - Uncompressed request size</item>
    ///     </list>
    ///     <para>
    ///         <b>Response attributes:</b>
    ///     </para>
    ///     <list type="bullet">
    ///         <item><c>http.status_code</c> - Response status code (200, 404, 500)</item>
    ///         <item><c>http.status_text</c> - Reason phrase (OK, Not Found)</item>
    ///         <item><c>http.response_content_length</c> - Response body size in bytes</item>
    ///         <item><c>http.response_content_length_uncompressed</c> - Uncompressed response size</item>
    ///     </list>
    /// </remarks>
    public static class OpenTelemetrySemantics
    {
        public const string OperationHttpClient = "http.client";
        public const string AttributeHttpMethod = "http.method";
        public const string AttributeHttpUrl = "http.url";
        public const string AttributeHttpTarget = "http.target";
        public const string AttributeHttpHost = "http.host";
        public const string AttributeHttpScheme = "http.scheme";
        public const string AttributeHttpStatusCode = "http.status_code";
        public const string AttributeHttpStatusText = "http.status_text";
        public const string AttributeHttpFlavor = "http.flavor";
        public const string AttributeHttpUserAgent = "http.user_agent";
        public const string AttributeHttpRequestContentLength = "http.request_content_length";
        public const string AttributeHttpRequestContentLengthUncompressed = "http.request_content_length_uncompressed";
        public const string AttributeHttpResponseContentLength = "http.response_content_length";
        public const string AttributeHttpResponseContentLengthUncompressed = "http.response_content_length_uncompressed";
        public const string AttributeHttpRequestMethod = "http.request.method";
        public const string AttributeHttpResponseStatusCode = "http.response.status_code";
    }
}
