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
        UniTask<(bool, string)> TryGetHashAsync(IWebRequestController webRequestController, Vector2Int coordinate, string reportCategory);
    }

    public class GetHashGoerli : IGetHash
    {
        public async UniTask<(bool, string)> TryGetHashAsync(IWebRequestController webRequestController, Vector2Int coordinate, string reportCategory)
        {
            try
            {
                var getAbout = await webRequestController.GetAsync(new CommonArguments(URLAddress.FromString("https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main-latest/about")), new CancellationToken(), reportCategory)
                    .CreateFromJson<ServerAbout>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

                foreach (string? contentDefinition in getAbout.configurations.scenesUrn)
                {
                    var getSceneDefinition = await webRequestController.GetAsync(new CommonArguments(URLAddress.FromString($"https://sdk-team-cdn.decentraland.org/ipfs/{IpfsHelper.ParseUrn(contentDefinition).EntityId}")), new CancellationToken(), reportCategory)
                        .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

                    if (getSceneDefinition.metadata.scene.DecodedParcels.Contains(coordinate))
                    {
                        string sceneHash = IpfsHelper.ParseUrn(contentDefinition).EntityId;
                        return (true, sceneHash);
                    }
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
        public async UniTask<(bool, string)> TryGetHashAsync(IWebRequestController webRequestController, Vector2Int coordinate, string reportCategory)
        {
            try
            {
                var getSceneDefinition = await webRequestController.PostAsync(new CommonArguments(URLAddress.FromString("https://peer.decentraland.org/content/entities/active/")), GenericPostArguments.CreateJson($"{{\"pointers\": [\"{coordinate.x},{coordinate.y}\" ]}}"),
                        new CancellationToken(), reportCategory)
                    .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);
                return (true, getSceneDefinition.id!);
            }
            catch (Exception _)
            {
            }

            ReportHub.LogError(reportCategory, $"Trying to load hybrid scene with coordinates {coordinate} failed. You wont get the asset bundles");
            return (false, "");
        }
    }
}