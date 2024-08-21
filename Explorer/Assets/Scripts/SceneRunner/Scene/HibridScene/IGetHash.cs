using System;
using System.Linq;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using UnityEngine;

namespace SceneRunner.Scene
{
    public interface IGetHash
    {
        UniTask<(bool, string)> TryGetHashAsync(IWebRequestController webRequestController, URLDomain contentDomain, Vector2Int coordinate, string reportCategory);
    }

    public class GetHashGoerli : IGetHash
    {
        public async UniTask<(bool, string)> TryGetHashAsync(IWebRequestController webRequestController, URLDomain contentDomain, Vector2Int coordinate, string reportCategory)
        {
            try
            {
                var getAbout = await webRequestController.GetAsync(new CommonArguments(URLAddress.FromString("https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main-latest/about")), new CancellationToken(), reportCategory)
                    .CreateFromJson<ServerAbout>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

                foreach (string? contentDefinition in getAbout.configurations.scenesUrn)
                {
                    var url = contentDomain.Append(URLPath.FromString(IpfsHelper.ParseUrn(contentDefinition).EntityId));

                    var getSceneDefinition = await webRequestController.GetAsync(new CommonArguments(url), new CancellationToken(), reportCategory)
                        .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

                    if (!getSceneDefinition.metadata.scene.DecodedParcels.Contains(coordinate)) continue;
                    string sceneHash = IpfsHelper.ParseUrn(contentDefinition).EntityId;
                    return (true, sceneHash);
                }
            }
            catch (Exception _)
            {
            }

            ReportHub.LogError(reportCategory, $"Trying to load hybrid scene with coordinates {coordinate} failed. You wont get the asset bundles");
            return (false, "");
        }
    }

    public class GetHashWorld : IGetHash
    {
        private readonly string world;

        public GetHashWorld(string world)
        {
            this.world = world;
        }

        public async UniTask<(bool, string)> TryGetHashAsync(IWebRequestController webRequestController, URLDomain contentDomain, Vector2Int coordinate, string reportCategory)
        {
            try
            {
                var getAbout = await webRequestController.GetAsync(new CommonArguments(URLAddress.FromString($"https://worlds-content-server.decentraland.org/world/{world}/about")), new CancellationToken(), reportCategory)
                    .CreateFromJson<ServerAbout>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

                foreach (string? contentDefinition in getAbout.configurations.scenesUrn)
                {
                    var sceneDefinitionURL = contentDomain.Append(URLPath.FromString(IpfsHelper.ParseUrn(contentDefinition).EntityId));

                    var getSceneDefinition = await webRequestController.GetAsync(new CommonArguments(sceneDefinitionURL), new CancellationToken(), reportCategory)
                        .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

                    if (!getSceneDefinition.metadata.scene.DecodedParcels.Contains(coordinate)) continue;
                    string sceneHash = IpfsHelper.ParseUrn(contentDefinition).EntityId;
                    return (true, sceneHash);
                }
            }
            catch (Exception _)
            {
            }

            ReportHub.LogError(reportCategory, $"Trying to load hybrid scene with coordinates {coordinate} failed. You wont get the asset bundles");
            return (false, "");
        }
    }

    public class GetHashGenesis : IGetHash
    {
        public async UniTask<(bool, string)> TryGetHashAsync(IWebRequestController webRequestController, URLDomain contentDomain, Vector2Int coordinate, string reportCategory)
        {
            try
            {
                var getSceneDefinition = await webRequestController.PostAsync(new CommonArguments(URLAddress.FromString("https://peer.decentraland.org/content/entities/active/")), GenericPostArguments.CreateJson($"{{\"pointers\": [\"{coordinate.x},{coordinate.y}\" ]}}"),
                        new CancellationToken(), reportCategory)
                    .CreateFromJson<SceneEntityDefinition[]>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);
                return (true, getSceneDefinition[0].id!);
            }
            catch (Exception _)
            {
            }

            ReportHub.LogError(reportCategory, $"Trying to load hybrid scene with coordinates {coordinate} failed. You wont get the asset bundles");
            return (false, "");
        }
    }
}