using Newtonsoft.Json.Linq;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     Behaviour hints for a tool (MCP spec 2025-06-18) surfaced in tools/list so an agent can reason
    ///     about a tool before calling it. Only hints set through the factory methods are emitted; the rest
    ///     are omitted rather than defaulted, because the spec's implicit defaults are surprising
    ///     (destructiveHint and openWorldHint default to true, readOnlyHint to false).
    /// </summary>
    public readonly struct McpToolAnnotations
    {
        private readonly bool? readOnlyHint;
        private readonly bool? destructiveHint;
        private readonly bool? idempotentHint;
        private readonly bool? openWorldHint;

        private McpToolAnnotations(bool? readOnlyHint, bool? destructiveHint, bool? idempotentHint, bool? openWorldHint)
        {
            this.readOnlyHint = readOnlyHint;
            this.destructiveHint = destructiveHint;
            this.idempotentHint = idempotentHint;
            this.openWorldHint = openWorldHint;
        }

        /// <summary>
        ///     A tool that only reads state. The spec ignores destructiveHint/idempotentHint when readOnlyHint
        ///     is true, so they are left unset. <paramref name="openWorld" /> stays false for the local Explorer.
        /// </summary>
        public static McpToolAnnotations ReadOnly(bool openWorld = false) =>
            new (readOnlyHint: true, destructiveHint: null, idempotentHint: null, openWorldHint: openWorld);

        /// <summary>
        ///     A tool that changes state. <paramref name="destructive" /> flags irreversible or data-losing
        ///     effects; <paramref name="idempotent" /> flags that repeating the call with the same arguments
        ///     has no additional effect. <paramref name="openWorld" /> stays false for the local Explorer.
        /// </summary>
        public static McpToolAnnotations Mutating(bool destructive, bool idempotent, bool openWorld = false) =>
            new (readOnlyHint: false, destructiveHint: destructive, idempotentHint: idempotent, openWorldHint: openWorld);

        /// <summary>Serializes the set hints to the MCP annotations object embedded in a tools/list entry.</summary>
        public JObject ToJObject()
        {
            var json = new JObject();

            if (readOnlyHint.HasValue) json["readOnlyHint"] = readOnlyHint.Value;
            if (destructiveHint.HasValue) json["destructiveHint"] = destructiveHint.Value;
            if (idempotentHint.HasValue) json["idempotentHint"] = idempotentHint.Value;
            if (openWorldHint.HasValue) json["openWorldHint"] = openWorldHint.Value;

            return json;
        }
    }
}
