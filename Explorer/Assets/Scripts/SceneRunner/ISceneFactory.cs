using Cysharp.Threading.Tasks;
using Ipfs;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;

namespace SceneRunner
{
    public interface ISceneFactory
    {
        /// <summary>
        /// Must be started on the main thread.
        /// Starts scripts downloading on the main thread because of UnityWebRequest limitations
        /// Then switches to the background thread for the rest of instantiations
        /// </summary>
        /// <param name="jsCodeUrl"></param>
        /// <param name="ct"></param>
        /// <returns>Scene Facade on the background thread</returns>
        UniTask<ISceneFacade> CreateScene(string jsCodeUrl, CancellationToken ct);

        /// <summary>
        ///     Create a scene from the directory with the scene.json file (just like it is in the goerli-plaza repo)
        /// </summary>
        /// <param name="directoryName"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        UniTask<ISceneFacade> CreateSceneFromStreamableDirectory(string directoryName, CancellationToken ct);

        /// <summary>
        ///     Creates a scene from the StreamingAssets/Scenes/ folder
        /// </summary>
        /// <param name="fileName">File name without JS extension</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        UniTask<ISceneFacade> CreateSceneFromStreamingAssets(string fileName, CancellationToken ct) =>
            CreateScene($"file://{Application.streamingAssetsPath}/Scenes/{fileName}.js", ct);

        /// <summary>
        ///     Creates a scene from the EntityDefinition
        /// </summary>
        /// <param name="ipfsRealm"></param>
        /// <param name="sceneDefinition">EntityDefinition provided by the ContentServer</param>
        /// <param name="abManifest"></param>
        /// <param name="ct"></param>
        /// <returns>Scene Facade on the background thread</returns>
        UniTask<ISceneFacade> CreateSceneFromSceneDefinition(IIpfsRealm ipfsRealm, IpfsTypes.SceneEntityDefinition sceneDefinition, SceneAssetBundleManifest abManifest, CancellationToken ct);
    }
}
