using System;

namespace DCL.Mcp.Transport
{
    /// <summary>
    ///     Rejects browser-originated cross-site requests (drive-by pages, DNS rebinding).
    ///     Requests without an Origin header (CLI clients like Claude Code) are allowed.
    /// </summary>
    public static class McpOriginValidator
    {
        public static bool IsAllowed(string? origin)
        {
            if (string.IsNullOrEmpty(origin))
                return true;

            if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? originUri))
                return false;

            if (originUri.Scheme != Uri.UriSchemeHttp && originUri.Scheme != Uri.UriSchemeHttps)
                return false;

            return originUri.Host is "localhost" or "127.0.0.1" or "::1";
        }
    }
}
