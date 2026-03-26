using CommunicationData.URLHelpers;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     Wraps <see cref="SceneHashedContent" /> and overrides the URL for the scene's main script
    ///     (usually <c>index.js</c>) to point to the asset bundle CDN instead of the catalyst.
    ///     This avoids relying on the catalyst for scenes already served from the asset bundle registry.
    /// </summary>
    /// <remarks>
    ///     <c>main.crdt</c> is handled separately in
    ///     <see cref="ECS.SceneLifeCycle.Systems.LoadSceneSystemLogicBase" /> with an async CDN-first
    ///     strategy and a catalyst fallback, so it is NOT overridden here.
    /// </remarks>
    public class SceneHashedContentWithCDN : ISceneContent
    {
        private readonly SceneHashedContent innerContent;
        private readonly string mainScriptFileName;
        private readonly URLAddress cdnMainScriptUrl;

        public URLDomain ContentBaseUrl => innerContent.ContentBaseUrl;

        public SceneHashedContentWithCDN(
            SceneHashedContent innerContent,
            string mainScriptFileName,
            URLAddress cdnMainScriptUrl)
        {
            this.innerContent = innerContent;
            this.mainScriptFileName = mainScriptFileName;
            this.cdnMainScriptUrl = cdnMainScriptUrl;
        }

        public bool TryGetContentUrl(string contentPath, out URLAddress result)
        {
            // Redirect the main script to the CDN URL
            if (string.Equals(contentPath, mainScriptFileName, System.StringComparison.OrdinalIgnoreCase))
            {
                result = cdnMainScriptUrl;
                return true;
            }

            return innerContent.TryGetContentUrl(contentPath, out result);
        }

        public bool TryGetHash(string name, out string hash) =>
            innerContent.TryGetHash(name, out hash);
    }
}
