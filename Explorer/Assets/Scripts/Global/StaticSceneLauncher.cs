using Diagnostics.ReportsHandling;
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
        [SerializeField] private ReportsHandlingSettings reportsHandlingSettings;

        public SceneSharedContainer SceneSharedContainer { get; private set; }

        private ISceneFacade sceneFacade;

        private void Awake()
        {
            SceneSharedContainer = Install(reportsHandlingSettings);
        }

        private void Start()
        {
            sceneLauncher.Initialize(SceneSharedContainer, destroyCancellationToken);
        }

        private void OnDestroy()
        {
            SceneSharedContainer?.Dispose();
        }

        public static SceneSharedContainer Install(IReportsHandlingSettings reportsHandlingSettings)
        {
            Profiler.BeginSample($"{nameof(DynamicSceneLoader)}.Install");

            var componentsContainer = ComponentsContainer.Create();
            var sceneSharedContainer = SceneSharedContainer.Create(componentsContainer, reportsHandlingSettings);

            Profiler.EndSample();
            return sceneSharedContainer;
        }
    }
}
