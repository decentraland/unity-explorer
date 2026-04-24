using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utility;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Components;
using Global.Dynamic;
using SceneRunner.Scene;
using System;
using System.Threading;

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

        protected override async UniTask<bool> OverrideSceneMetadataAsync(ISceneContent sceneContent, GetSceneFacadeIntention intention, ReportData reportCategory, string sceneID, CancellationToken ct)
        {
            bool success = await base.OverrideSceneMetadataAsync(sceneContent, intention, reportCategory, sceneID, ct);

            if (success && hybridSceneHashedContent?.remoteSceneID != null)
                intention.DefinitionComponent.Definition.id = hybridSceneHashedContent.remoteSceneID;

            return success;
        }

        protected override async UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, ReportData reportCategory, CancellationToken ct)
        {
            hybridSceneHashedContent = new HybridSceneHashedContent(webRequestController, definition, contentBaseUrl, assetBundleURL);

            if (await hybridSceneHashedContent.TryGetRemoteSceneIDAsync(hybridSceneContentServerDomain,
                    hybridSceneContentServer, definition.metadata.scene.DecodedBase, world, reportCategory, ct, worldContentServerBaseUrl.Value))
            {
                await hybridSceneHashedContent.GetRemoteSceneDefinitionAsync(hybridSceneContentServerDomain,
                    reportCategory, ct);

                await FetchRemoteAssetBundleVersion(definition, reportCategory, ct);
            }

            return hybridSceneHashedContent;
        }

        private async UniTask FetchRemoteAssetBundleVersion(SceneEntityDefinition definition, ReportData reportCategory, CancellationToken ct)
        {
            try
            {
                string sceneId = hybridSceneHashedContent?.remoteSceneID!;
                string manifestPath = $"manifest/{sceneId}{PlatformUtils.GetCurrentPlatform()}.json";
                URLAddress manifestUrl = assetBundleURL.Append(URLPath.FromString(manifestPath));

                SceneAbDto manifest = await webRequestController.GetAsync(new CommonArguments(manifestUrl), ct, reportCategory)
                                                                .CreateFromJson<SceneAbDto>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchBackToMainThread);

                definition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateFromFallback(manifest.Version, manifest.Date);
                definition.assetBundleManifestVersion.InjectContent(sceneId, definition.content);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.SCENE_LOADING); }
        }
    }
}
