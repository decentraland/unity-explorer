using Diagnostics.ReportsHandling;
using Global.Dynamic;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Profiling;

namespace Global.Static
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

            var staticContainer = StaticContainer.Create(new NoPartitionSettings(), reportsHandlingSettings);
            var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer, null);

            Profiler.EndSample();
            return sceneSharedContainer;
        }
    }
}
