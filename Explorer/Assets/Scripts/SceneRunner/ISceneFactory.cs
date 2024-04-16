using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using ECS.Prioritization.Components;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;

namespace SceneRunner
{
    public interface ISceneFactory
    {
        /// <summary>
        ///     Must be started on the main thread.
        ///     Starts scripts downloading on the main thread because of UnityWebRequest limitations
        ///     Then switches to the background thread for the rest of instantiations
        /// </summary>
        /// <param name="jsCodeUrl"></param>
        /// <param name="partitionProvider"></param>
        /// <param name="ct"></param>
        /// <returns>Scene Facade on the background thread</returns>
        UniTask<ISceneFacade> CreateSceneFromFileAsync(string jsCodeUrl, IPartitionComponent partitionProvider, CancellationToken ct);

        /// <summary>
        ///     Create a scene from the directory with the scene.json file (just like it is in the goerli-plaza repo)
        /// </summary>
        /// <param name="directoryName"></param>
        /// <param name="partitionProvider"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        UniTask<ISceneFacade> CreateSceneFromStreamableDirectoryAsync(string directoryName, IPartitionComponent partitionProvider, CancellationToken ct);

        /// <summary>
        ///     Creates a scene from the StreamingAssets/Scenes/ folder
        /// </summary>
        /// <param name="fileName">File name without JS extension</param>
        /// <param name="partitionProvider"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        UniTask<ISceneFacade> CreateSceneFromStreamingAssets(string fileName, IPartitionComponent partitionProvider, CancellationToken ct) =>
            CreateSceneFromFileAsync($"file://{Application.streamingAssetsPath}/Scenes/{fileName}.js", partitionProvider, ct);

        /// <summary>
        ///     Creates a scene from the EntityDefinition
        /// </summary>
        /// <param name="sceneData"></param>
        /// <param name="partitionProvider"></param>
        /// <param name="ct"></param>
        /// <returns>Scene Facade on the background thread</returns>
        UniTask<ISceneFacade> CreateSceneFromSceneDefinition(ISceneData sceneData, IPartitionComponent partitionProvider, CancellationToken ct);

        /// <summary>
        ///     Used for passing actions from the global world to the scene world
        /// </summary>
        /// <param name="actions"></param>
        void SetGlobalWorldActions(IGlobalWorldActions actions);
    }
}
