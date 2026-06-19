namespace SceneRunner.Scene
{
    public class SceneAssetBundleManifest
    {
        private readonly string version;
        private readonly string buildDate;
        private readonly string[]? files;

        public SceneAssetBundleManifest(string version, string buildDate, string[]? files = null)
        {
            this.version = version;
            this.buildDate = buildDate;
            this.files = files;
        }

        public string GetVersion() =>
            version;

        public string GetBuildDate() =>
            buildDate;

        public string[]? GetFiles() =>
            files;
    }
}
