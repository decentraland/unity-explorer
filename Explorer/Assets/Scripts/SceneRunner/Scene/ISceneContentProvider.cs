namespace SceneRunner.Scene
{
    public interface ISceneContentProvider
    {
        /// <summary>
        ///     Translates URL encoded in SDK components into a path in the scene bundle
        ///     from which an asset can be downloaded from
        /// </summary>
        bool TryGetContentUrl(string url, out string result);
    }
}
