using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    public class LocalSceneDevelopmentSceneRoomMetaDataSource : ISceneRoomMetaDataSource
    {
        private readonly IWebRequestController webRequestController;

        public LocalSceneDevelopmentSceneRoomMetaDataSource(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public bool ScenesCommunicationIsIsolated => false;

        public bool MetadataIsDirty => false;

        public MetaData.Input GetMetadataInput() =>
            new ("LocalSceneDevelopment", Vector2Int.zero);

        public async UniTask<Result<MetaData>> MetaDataAsync(MetaData.Input input, CancellationToken token)
        {
            URLDomain baseUrl = URLDomain.FromString(IRealmNavigator.LOCALHOST);
            URLAddress sceneDefinitionEndpoint = baseUrl.Append(URLSubdirectory.FromString("scene.json"));
            URLAddress idEndpoint = baseUrl.Append(URLSubdirectory.FromString("content/entities/active"));

            SceneDefinition sceneDefinition =
                await webRequestController.GetAsync(
                                               new CommonArguments(sceneDefinitionEndpoint),
                                               token,
                                               ReportCategory.LIVEKIT
                                           )
                                          .CreateFromJson<SceneDefinition>(WRJsonParser.Unity);

            var baseResult = sceneDefinition.scene.BaseParcel();

            if (baseResult.Has == false)
                return Result<MetaData>.ErrorResult("Cannot get base parcel from scene definition");

            Vector2Int coordinate = baseResult.Value;

            EndpointResponse[]? result =
                await webRequestController.PostAsync(
                                               new CommonArguments(idEndpoint),
                                               GenericPostArguments.CreateJson($"{{\"pointers\": [\"{coordinate.x},{coordinate.y}\" ]}}"),
                                               token,
                                               ReportCategory.LIVEKIT
                                           )
                                          .CreateFromJson<EndpointResponse[]>(WRJsonParser.Newtonsoft);

            if (result == null)
                return Result<MetaData>.ErrorResult($"Error result from: {idEndpoint}");

            if (result.Length == 0)
                return Result<MetaData>.ErrorResult($"Empty array from endpoint: {idEndpoint}");

            string? id = result[0].id;

            if (string.IsNullOrWhiteSpace(id!))
                return Result<MetaData>.ErrorResult("Id is empty or null");

            // TODO Remove later
            // using HashKey key = HashKey.FromString(id);
            // id = HashUtility.ByteString(key.Hash.Memory).EnsureNotNull();

            //id = id.Replace("-", string.Empty).Replace("=", string.Empty);

            //id = id.Substring(0, 64);

            return Result<MetaData>.SuccessResult(new MetaData(id, new MetaData.Input(id, Vector2Int.zero)));
        }

        [Serializable]
        private struct EndpointResponse
        {
            public string? id;
        }

        [Serializable]
        private struct SceneDefinition
        {
            public Scene scene;
        }

        [Serializable]
        private struct Scene
        {
            public string @base;

            public Option<Vector2Int> BaseParcel()
            {
                if (string.IsNullOrWhiteSpace(@base))
                    return Option<Vector2Int>.None;

                string[]? parts = @base.Split(',');

                if (parts == null)
                    return Option<Vector2Int>.None;

                if (parts.Length < 2)
                    return Option<Vector2Int>.None;

                string rawX = parts[0];
                string rawY = parts[1];

                if (int.TryParse(rawX, out int x) == false)
                    return Option<Vector2Int>.None;

                if (int.TryParse(rawY, out int y) == false)
                    return Option<Vector2Int>.None;

                Vector2Int result = new Vector2Int(x, y);
                return Option<Vector2Int>.Some(result);
            }
        }
    }
}
