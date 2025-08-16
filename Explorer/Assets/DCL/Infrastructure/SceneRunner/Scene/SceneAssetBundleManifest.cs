namespace SceneRunner.Scene
{
    public class SceneAssetBundleManifest
    {
        private readonly string version;
        private readonly string buildDate;

        public SceneAssetBundleManifest(string version, string buildDate)
        {
            this.version = version;
            this.buildDate = buildDate;
        }

        public string GetVersion() =>
            version;

        public string GetBuildDate() =>
            buildDate;
    }
}
