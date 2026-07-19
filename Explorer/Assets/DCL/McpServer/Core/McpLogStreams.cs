namespace DCL.McpServer.Core
{
    /// <summary>
    ///     The two log streams a client can subscribe to over the SSE GET endpoint (<c>?stream=</c>).
    ///     Shared so the transport (which validates the query) and the notifier (which publishes) agree.
    /// </summary>
    public static class McpLogStreams
    {
        /// <summary>The running SDK7 scene's JavaScript console.</summary>
        public const string SCENE = "scene";

        /// <summary>The Unity player/editor log — engine, build and editor output.</summary>
        public const string CLIENT = "client";

        public static bool IsKnown(string? stream) =>
            stream is SCENE or CLIENT;
    }
}
