using Cysharp.Threading.Tasks;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Profiling;

namespace Global
{
    /// <summary>
    /// An entry point to install and resolve all dependencies
    /// </summary>
    public class EntryPoint : MonoBehaviour
    {
        public SceneSharedContainer SceneSharedContainer { get; private set; }

        private ISceneFacade sceneFacade;

        private void Awake()
        {
            SceneSharedContainer = Install();
        }

        private void Start()
        {
            async UniTask CreateScene()
            {
                sceneFacade = await SceneSharedContainer.SceneFactory.CreateScene
                    ($"file://{Application.dataPath + "/../TestResources/Scenes/CubeWave/cube_waves.js"}", destroyCancellationToken);

                sceneFacade.StartUpdateLoop(30, destroyCancellationToken);
            }

            CreateScene().Forget();
        }

        private void OnDestroy()
        {
            sceneFacade?.Dispose();
        }

        public static SceneSharedContainer Install()
        {
            Profiler.BeginSample($"{nameof(EntryPoint)}.Install");

            var componentsContainer = ComponentsContainer.Create();
            var sceneSharedContainer = SceneSharedContainer.Create(componentsContainer);

            Profiler.EndSample();
            return sceneSharedContainer;
        }
    }
}
