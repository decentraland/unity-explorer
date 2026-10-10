using System;
using System.Buffers;

namespace DCL.Ipfs
{
    /// <summary>
    ///     Classifies the "b64-" content hashes minted by the local scene development server.
    ///     Newer dev servers version each hash by embedding the file's modification time — a NUL byte
    ///     separates the path from the version inside the base64 payload (see @dcl/sdk-commands
    ///     <c>b64ContentVersionedHashingFunction</c>) — so an edited file gets a new hash. Older dev
    ///     servers hash the path alone, so an edited file keeps its hash. The dev server hashes every
    ///     file the same way, so the first content entry decides for the whole scene.
    /// </summary>
    public static class LocalSceneDevHashes
    {
        private const string PREFIX = "b64-";

        /// <summary>
        ///     True when the scene's hashes embed the file modification time: an edited file changes
        ///     its hash, so stale cache entries are unreachable by construction.
        /// </summary>
        public static bool IsContentVersioned(SceneEntityDefinition? definition) =>
            Classify(definition) == HashKind.ContentVersioned;

        /// <summary>
        ///     True when the scene's hashes derive from the file path alone: an edited file keeps its
        ///     hash, so cache entries keyed on it can serve stale or invalidated content.
        /// </summary>
        public static bool IsPathOnly(SceneEntityDefinition? definition) =>
            Classify(definition) == HashKind.PathOnly;

        private enum HashKind
        {
            /// <summary>Not a local-dev hash at all (e.g. a production content-addressed hash).</summary>
            NotLocalDev,
            PathOnly,
            ContentVersioned,
        }

        private static HashKind Classify(SceneEntityDefinition? definition)
        {
            ContentDefinition[]? content = definition?.content;

            if (content == null || content.Length == 0)
                return HashKind.NotLocalDev;

            return Classify(content[0].hash);
        }

        private static HashKind Classify(string? hash)
        {
            if (string.IsNullOrEmpty(hash) || !hash.StartsWith(PREFIX, StringComparison.Ordinal))
                return HashKind.NotLocalDev;

            ReadOnlySpan<char> payload = hash.AsSpan(PREFIX.Length);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(payload.Length);

            try
            {
                // A path-only hash decodes to "{path}-{machineId}"; a versioned one to
                // "{path}\0{mtimeMs}-{machineId}". The NUL cannot occur in a path or hostname, so its
                // presence in the decoded bytes uniquely marks the versioned format.
                if (!Convert.TryFromBase64Chars(payload, buffer, out int written))
                    return HashKind.NotLocalDev;

                return Array.IndexOf(buffer, (byte)0, 0, written) >= 0
                    ? HashKind.ContentVersioned
                    : HashKind.PathOnly;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
