using CommunicationData.URLHelpers;
using System;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     Wraps <see cref="SceneHashedContent" /> and redirects <c>index.js</c>, <c>scene.json</c>,
    ///     and <c>main.crdt</c> to the asset bundle CDN instead of the catalyst.
    ///     Only used when all three files have been validated via HEAD requests.
    /// </summary>
    public class SceneHashedContentWithCDN : ISceneContent
    {
        private const string SCENE_JSON = "scene.json";
        private const string MAIN_CRDT = "main.crdt";

        private readonly SceneHashedContent innerContent;
        private readonly string mainScriptFileName;
        private readonly URLDomain cdnBaseUrl;
        private readonly string cdnBasePath;

        public URLDomain ContentBaseUrl => innerContent.ContentBaseUrl;

        public SceneHashedContentWithCDN(
            SceneHashedContent innerContent,
            string mainScriptFileName,
            URLDomain cdnBaseUrl,
            string cdnBasePath)
        {
            this.innerContent = innerContent;
            this.mainScriptFileName = mainScriptFileName;
            this.cdnBaseUrl = cdnBaseUrl;
            this.cdnBasePath = cdnBasePath;
        }

        public bool TryGetContentUrl(string contentPath, out URLAddress result)
        {
            if (string.Equals(contentPath, mainScriptFileName, StringComparison.OrdinalIgnoreCase))
            {
                result = cdnBaseUrl.Append(URLPath.FromString($"{cdnBasePath}index.js"));
                return true;
            }

            if (string.Equals(contentPath, SCENE_JSON, StringComparison.OrdinalIgnoreCase))
            {
                result = cdnBaseUrl.Append(URLPath.FromString($"{cdnBasePath}{SCENE_JSON}"));
                return true;
            }

            if (string.Equals(contentPath, MAIN_CRDT, StringComparison.OrdinalIgnoreCase))
            {
                result = cdnBaseUrl.Append(URLPath.FromString($"{cdnBasePath}{MAIN_CRDT}"));
                return true;
            }

            return innerContent.TryGetContentUrl(contentPath, out result);
        }

        public bool TryGetHash(string name, out string hash) =>
            innerContent.TryGetHash(name, out hash);
    }
}
