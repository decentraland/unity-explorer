using DCL.Ipfs;

namespace SceneRunner.Scene
{
    public class SceneAssetBundleManifest
    {
        private readonly string version;
        private readonly string buildDate;
        private readonly string[]? files;
        private readonly SceneAbLodsDto? lods;

        public SceneAssetBundleManifest(string version, string buildDate, string[]? files = null, SceneAbLodsDto? lods = null)
        {
            this.version = version;
            this.buildDate = buildDate;
            this.files = files;
            this.lods = lods;
        }

        public string GetVersion() =>
            version;

        public string GetBuildDate() =>
            buildDate;

        public string[]? GetFiles() =>
            files;

        public SceneAbLodsDto? GetLods() =>
            lods;
    }
}
