using Cysharp.Threading.Tasks;
using Global;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;

public class TestScene : MonoBehaviour
{
    private SceneSharedContainer sceneSharedContainer;
    private ISceneFacade sceneFacade;
    private UpdateTransformSystem updateTransformSystem;
    private InstantiateUnityTransforms instantiateUnityTransforms;
    private string path;
    private bool initDone;

    // Start is called before the first frame update
    private void Start()
    {
        sceneSharedContainer = EntryPoint.Install();
        path = $"file://{Application.dataPath + "/../TestResources/Scenes/CubeWave/cube_waves.js"}";
        EmitECSComponents().Forget();
    }

    public async UniTask EmitECSComponents()
    {
        sceneFacade = await sceneSharedContainer.SceneFactory.CreateScene(path, CancellationToken.None);
        sceneFacade.StartUpdateLoop(24, CancellationToken.None);
    }
}
