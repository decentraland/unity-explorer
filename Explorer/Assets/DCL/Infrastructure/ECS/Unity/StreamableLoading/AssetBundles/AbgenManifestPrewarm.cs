#nullable enable

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Hand-off of the abgen sidecar's already-fetched scene manifest to the scene loading lane.
    ///     Boot holds on the warm-up's manifest request and the reconversion watcher re-fetches it after
    ///     every content edit, so when the scene lane asks for the same URL the response is already in
    ///     hand — reusing it removes the server's content revalidation (seconds on a large scene) from
    ///     the scene-entry critical path. Single entry: local scene development serves exactly one scene.
    /// </summary>
    public static class AbgenManifestPrewarm
    {
        private static readonly object GATE = new ();

        private static string? url;
        private static string? json;

        /// <summary>Stores the manifest response fetched from <paramref name="manifestUrl" />, replacing any previous entry.</summary>
        public static void Set(string manifestUrl, string manifestJson)
        {
            lock (GATE)
            {
                url = manifestUrl;
                json = manifestJson;
            }
        }

        /// <summary>Drops the stored manifest. Called on a preview content edit, which may change the file census.</summary>
        public static void Invalidate()
        {
            lock (GATE)
            {
                url = null;
                json = null;
            }
        }

        /// <summary>True when a manifest fetched from exactly <paramref name="manifestUrl" /> is held; the entry stays for scene reloads.</summary>
        public static bool TryGet(string manifestUrl, out string manifestJson)
        {
            lock (GATE)
            {
                if (url == manifestUrl && json != null)
                {
                    manifestJson = json;
                    return true;
                }

                manifestJson = string.Empty;
                return false;
            }
        }
    }
}
