using System.Collections.Generic;
using Utility;

namespace SceneRunner.Scene
{
    public class SceneAssetBundleManifest
    {
        // 32 hex chars in `<hash>_<depsDigest>_<platform>` filenames produced by AB-converter v49+
        private const int DEPS_DIGEST_LENGTH = 32;
        private static readonly char[] FILE_NAME_SEPARATOR = { '_' };

        private readonly string version;
        private readonly string buildDate;
        private readonly IReadOnlyDictionary<string, string>? depsDigests;

        public SceneAssetBundleManifest(string version, string buildDate, IReadOnlyDictionary<string, string>? depsDigests = null)
        {
            this.version = version;
            this.buildDate = buildDate;
            this.depsDigests = depsDigests;
        }

        public string GetVersion() =>
            version;

        public string GetBuildDate() =>
            buildDate;

        public IReadOnlyDictionary<string, string>? GetDepsDigests() =>
            depsDigests;

        public bool TryGetDepsDigest(string hash, out string digest)
        {
            if (depsDigests != null && depsDigests.TryGetValue(hash, out digest!))
                return true;

            digest = string.Empty;
            return false;
        }

        public static IReadOnlyDictionary<string, string>? ExtractDepsDigests(string[]? files)
        {
            if (files == null || files.Length == 0)
                return null;

            Dictionary<string, string>? map = null;

            for (var i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (string.IsNullOrEmpty(file)) continue;

                // Expect "<hash>_<depsDigest>_<platform>". Anything else (including legacy 2-part filenames) is skipped.
                string[] parts = file.Split(FILE_NAME_SEPARATOR, 3);
                if (parts.Length < 3) continue;

                string digest = parts[1];
                if (digest.Length != DEPS_DIGEST_LENGTH || !IsHex(digest)) continue;

                map ??= new Dictionary<string, string>(new UrlHashComparer());
                map[parts[0]] = digest;
            }

            return map;
        }

        private static bool IsHex(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }
            return true;
        }
    }
}
