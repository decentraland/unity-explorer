using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.SceneFacade;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadSmartWearablePreviewSceneSystem : BaseUnityLoopSystem
    {
        private readonly IWebRequestController webRequestController;

        public LoadSmartWearablePreviewSceneSystem(World world, IWebRequestController webRequestController) : base(world)
        {
            this.webRequestController = webRequestController;
        }

        protected override void Update(float t)
        {
            LoadPreviewSceneQuery(World);
            PrepareForReloadingQuery(World);
        }

        [Query]
        [None(typeof(SmartWearablePreviewScene))]
        private void LoadPreviewScene(Entity entity, RealmComponent realm)
        {
            World.Add(entity, new SmartWearablePreviewScene { Value = Entity.Null });

            TryLoadPreviewSceneAsync(entity, realm.Ipfs, CancellationToken.None).Forget();
        }

        [Query]
        private void PrepareForReloading(Entity entity, in SmartWearablePreviewScene previewScene)
        {
            if (previewScene.Value == Entity.Null || World.IsAlive(previewScene.Value)) return;

            // Scene loaded but the corresponding entity doesn't exist anymore
            // Prepare for reloading

            World.Remove<SmartWearablePreviewScene>(entity);
        }

        private async UniTask TryLoadPreviewSceneAsync(Entity realm, IIpfsRealm ipfs, CancellationToken ct)
        {
            string url = URLBuilder.Combine(ipfs.CatalystBaseUrl, URLSubdirectory.FromString("preview-wearables")).Value;

            var args = new CommonLoadingArguments(URLAddress.FromString(url));
            var response = await webRequestController.GetAsync(args, ct, ReportCategory.WEARABLE, ignoreErrorCodes: new HashSet<long> { 404 })
                                                     .CreateFromJson<PreviewWearablesResponse>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);


            if (ct.IsCancellationRequested || !response.ok) return;

            // If no wearables are returned, it just means we are not previewing a wearable, no need to log an error
            if (response.data.Count == 0)  return;

            // Can safely assume it's only 1 wearable that it's being previewed
            PreviewWearablesResponse.Wearable wearable = response.data[0];

            if (!TryGetSceneJsonUrl(wearable, out string sceneJsonUrl))
            {
                ReportHub.LogError(GetReportCategory(), $"The previewed Wearable '{wearable.id}' does not contain the required 'scene.json' asset");
                return;
            }

            args = new  CommonLoadingArguments(sceneJsonUrl);
            var sceneMetadata = await webRequestController.GetAsync(args, ct, ReportCategory.WEARABLE)
                                                          .CreateFromJson<SceneMetadata>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);
            if (ct.IsCancellationRequested) return;

            // Since the ID is also used as part of a path, we encode it in b64 to remove illegal characters
            string sceneId = Convert.ToBase64String(Encoding.UTF8.GetBytes(wearable.id));
            var definition = new SceneEntityDefinition(sceneId, sceneMetadata)
            {
                content = wearable.data.representations[0].contents
                                  .Select(PreviewWearablesResponse.WearableContent.ToContentDefinition)
                                  .ToArray(),
            };
            var ipfsPath = new IpfsPath(definition.id!, URLDomain.EMPTY);

            // NOTICE that when creating the scene we do NOT mark it as a PX because we are running it as a normal scene
            SceneDefinitionComponent definitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(definition, ipfsPath);

            await UniTask.SwitchToMainThread();

            Entity scene = World.Create(definitionComponent, PartitionComponent.TOP_PRIORITY);
            CreateSceneFacadePromise.Execute(World, scene, ipfs, definitionComponent, PartitionComponent.TOP_PRIORITY);

            World.Set(realm, new SmartWearablePreviewScene { Value = scene });
        }

        private bool TryGetSceneJsonUrl(in PreviewWearablesResponse.Wearable wearable, out string sceneJsonUrl)
        {
            // For the time being default to the 1st representation we find
            var contents = wearable.data.representations[0].contents;

            foreach (var contentItem in contents)
                if (contentItem.key.EndsWith("scene.json", StringComparison.OrdinalIgnoreCase))
                {
                    sceneJsonUrl = contentItem.url;
                    return true;
                }

            sceneJsonUrl = string.Empty;
            return false;
        }

        /// <summary>
        ///     Attached to the realm entity.
        ///     Signals to the system that scene loading has started and stores a reference to the loaded scene entity.
        /// </summary>
        private struct SmartWearablePreviewScene
        {
            /// <summary>
            ///     The scene entity that was loaded.
            /// </summary>
            public Entity Value;
        }

        /// <summary>
        ///     Used to parse the json response from the /preview-wearables endpoint.
        /// </summary>
        [Serializable]
        private struct PreviewWearablesResponse
        {
            public bool ok;
            public List<Wearable> data;

            [Serializable]
            public struct Wearable
            {
                public string id;
                public WearableData data;
            }

            [Serializable]
            public struct WearableData
            {
                public List<WearableRepresentation> representations;
            }

            [Serializable]
            public struct WearableRepresentation
            {
                public List<WearableContent> contents;
            }

            [Serializable]
            public struct WearableContent
            {
                public string key;
                public string url;
                public string hash;

                public static ContentDefinition ToContentDefinition(WearableContent content)
                {
                    string key = GetKeyWithoutBodyShapePrefix(content);
                    return new ContentDefinition { file = key, hash = content.hash };
                }

                private static string GetKeyWithoutBodyShapePrefix(WearableContent content)
                {
                    string key = content.key;
                    if (key.StartsWith("male/", StringComparison.OrdinalIgnoreCase)) key = key.Replace("male/", string.Empty);
                    else if (key.StartsWith("female/", StringComparison.OrdinalIgnoreCase)) key = key.Replace("female/", string.Empty);
                    return key;
                }
            }
        }
    }
}
