using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Profiling;

namespace Global
{
    /// <summary>
    /// An entry point to install and resolve all dependencies
    /// </summary>
    public class StaticSceneLauncher : MonoBehaviour
    {
        [SerializeField] private SceneLauncher sceneLauncher;

        public SceneSharedContainer SceneSharedContainer { get; private set; }

        private ISceneFacade sceneFacade;

        private void Awake()
        {
            SceneSharedContainer = Install();
        }

        private void Start()
        {
            sceneLauncher.Initialize(SceneSharedContainer, destroyCancellationToken);
        }

        public static SceneSharedContainer Install()
        {
            Profiler.BeginSample($"{nameof(DynamicSceneLoader)}.Install");

            var componentsContainer = ComponentsContainer.Create();
            var sceneSharedContainer = SceneSharedContainer.Create(componentsContainer, null);

            Profiler.EndSample();
            return sceneSharedContainer;
        }
    }
}
