using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utility;
using DCL.WebRequests;
using Global.Dynamic;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadHybridSceneSystemLogic : LoadSceneSystemLogicBase
    {
        private static readonly URLDomain GOERLI_CONTENT_URL = URLDomain.FromString("https://sdk-team-cdn.decentraland.org/ipfs/");
        private static readonly URLDomain GENESIS_CONTENT_URL = URLDomain.FromString("https://peer.decentraland.org/content/contents/");
        private readonly HybridSceneContentServer hybridSceneContentServer;
        private readonly URLDomain hybridSceneContentServerDomain;
        private readonly string world;
        private readonly URLDomain worldContentServerBaseUrl;
        private HybridSceneHashedContent? hybridSceneHashedContent;
        private string? remoteSceneID;

        public LoadHybridSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL, HybridSceneParams hybridSceneParams, URLDomain worldContentServerContentsUrl, URLDomain worldContentServerBaseUrl) : base(webRequestController, assetBundleURL)
        {
            world = hybridSceneParams.World;
            hybridSceneContentServer = hybridSceneParams.HybridSceneContentServer;
            this.worldContentServerBaseUrl = worldContentServerBaseUrl;

            switch (hybridSceneContentServer)
            {
                case HybridSceneContentServer.Goerli:
                    hybridSceneContentServerDomain = GOERLI_CONTENT_URL;
                    break;
                case HybridSceneContentServer.Genesis:
                    hybridSceneContentServerDomain = GENESIS_CONTENT_URL;
                    break;
                case HybridSceneContentServer.World:
                    hybridSceneContentServerDomain = worldContentServerContentsUrl;
                    break;
            }
        }

        protected override string GetAssetBundleSceneId(string _) =>
            hybridSceneHashedContent!.remoteSceneID;

        protected override void PostProcessDefinition(SceneEntityDefinition definition)
        {
            if (hybridSceneHashedContent?.remoteSceneID != null)
                definition.id = hybridSceneHashedContent.remoteSceneID;
        }

        protected override async UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, ReportData reportCategory)
        {
            hybridSceneHashedContent = new HybridSceneHashedContent(webRequestController, definition, contentBaseUrl, assetBundleURL);

            if (await hybridSceneHashedContent.TryGetRemoteSceneIDAsync(hybridSceneContentServerDomain,
                    hybridSceneContentServer, definition.metadata.scene.DecodedBase, world, reportCategory, worldContentServerBaseUrl.Value))
            {
                await hybridSceneHashedContent.GetRemoteSceneDefinitionAsync(hybridSceneContentServerDomain,
                    reportCategory);

                await FetchRemoteManifestAsync(definition, reportCategory);
            }

            return hybridSceneHashedContent;
        }

        private async UniTask FetchRemoteManifestAsync(SceneEntityDefinition definition, ReportData reportCategory)
        {
            try
            {
                string manifestPath = $"manifest/{definition.id}{PlatformUtils.GetCurrentPlatform()}.json";
                URLAddress manifestUrl = assetBundleURL.Append(URLPath.FromString(manifestPath));

                UnityEngine.Debug.Log($"[HybridScene] Fetching AB manifest from {manifestUrl.Value}");

                SceneAbDto manifest = await webRequestController.GetAsync(
                        new CommonArguments(manifestUrl, RetryPolicy.WithRetries(1)), CancellationToken.None, reportCategory)
                    .CreateFromJson<SceneAbDto>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchBackToMainThread);

                definition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateFromFallback(manifest.Version, manifest.Date);
                definition.assetBundleManifestVersion.InjectContent(definition.id, definition.content);

                UnityEngine.Debug.Log($"[HybridScene] AB manifest version: {manifest.Version}, date: {manifest.Date}");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[HybridScene] Failed to fetch AB manifest: {e.Message}");
            }
        }
    }
}
