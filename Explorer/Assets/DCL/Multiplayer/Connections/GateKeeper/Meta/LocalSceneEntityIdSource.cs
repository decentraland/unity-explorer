using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utility.Types;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

// ReSharper disable InconsistentNaming
namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    /// <summary>
    ///     The scene entity a local development server serves for the project it previews.
    /// </summary>
    public readonly struct LocalSceneEntity
    {
        /// <summary>
        ///     The preview entity id minted by <c>sdk-commands</c> from the project path and the machine id.
        ///     Identifies the dev process, and is stable across content edits, hot reloads and server restarts.
        /// </summary>
        public readonly string Id;

        public readonly Vector2Int BaseParcel;

        public LocalSceneEntity(string id, Vector2Int baseParcel)
        {
            Id = id;
            BaseParcel = baseParcel;
        }
    }

    /// <summary>
    ///     Resolves the entity the local development server currently serves. Both transports key their
    ///     local-development session off it — LiveKit as the gatekeeper scene-room id, Pulse as the realm —
    ///     so it is defined once here rather than fetched independently by each.
    /// </summary>
    public interface ILocalSceneEntityIdSource
    {
        UniTask<Result<LocalSceneEntity>> EntityAsync(CancellationToken ct);
    }

    public class LocalSceneEntityIdSource : ILocalSceneEntityIdSource
    {
        private readonly IWebRequestController webRequestController;
        private readonly string realm;

        // realm is the local scene development realm the client was launched with (the `realm` deep link
        // parameter). The scene server only listens on the port it was started with, so falling back to the
        // default would make this source unreachable for any `sdk-commands start --port` but the default one.
        public LocalSceneEntityIdSource(IWebRequestController webRequestController, string? realm = null)
        {
            this.webRequestController = webRequestController;
            this.realm = string.IsNullOrWhiteSpace(realm) ? IRealmNavigator.LOCALHOST : realm;
        }

        public async UniTask<Result<LocalSceneEntity>> EntityAsync(CancellationToken ct)
        {
            URLDomain baseUrl = URLDomain.FromString(realm);
            URLAddress sceneDefinitionEndpoint = baseUrl.Append(URLSubdirectory.FromString("scene.json"));
            URLAddress idEndpoint = baseUrl.Append(URLSubdirectory.FromString("content/entities/active"));

            SceneDefinition sceneDefinition;

            try
            {
                sceneDefinition =
                    await webRequestController.GetAsync(
                                                   new CommonArguments(sceneDefinitionEndpoint),
                                                   ct,
                                                   ReportCategory.MULTIPLAYER,
                                                   suppressErrors: true
                                               )
                                              .CreateFromJson<SceneDefinition>(WRJsonParser.Unity);
            }
            catch (UnityWebRequestException e) when (e.Result == UnityWebRequest.Result.ConnectionError)
            {
                return Result<LocalSceneEntity>.ErrorResult($"Local scene server unreachable at {baseUrl}: {e.Message}");
            }

            Option<Vector2Int> baseResult = sceneDefinition.scene.BaseParcel();

            if (baseResult.Has == false)
                return Result<LocalSceneEntity>.ErrorResult("Cannot get base parcel from scene definition");

            Vector2Int coordinate = baseResult.Value;

            EndpointResponse[]? result =
                await webRequestController.PostAsync(
                                               new CommonArguments(idEndpoint),
                                               GenericPostArguments.CreateJson($"{{\"pointers\": [\"{coordinate.x},{coordinate.y}\" ]}}"),
                                               ct,
                                               ReportCategory.MULTIPLAYER
                                           )
                                          .CreateFromJson<EndpointResponse[]>(WRJsonParser.Newtonsoft);

            if (result == null)
                return Result<LocalSceneEntity>.ErrorResult($"Error result from: {idEndpoint}");

            if (result.Length == 0)
                return Result<LocalSceneEntity>.ErrorResult($"Empty array from endpoint: {idEndpoint}");

            string? id = result[0].id;

            if (string.IsNullOrWhiteSpace(id))
                return Result<LocalSceneEntity>.ErrorResult("Id is empty or null");

            return Result<LocalSceneEntity>.SuccessResult(new LocalSceneEntity(id, coordinate));
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
