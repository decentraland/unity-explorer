using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Profiling;
using Utility;

namespace Global
{
    /// <summary>
    /// An entry point to install and resolve all dependencies
    /// </summary>
    public class EntryPoint : MonoBehaviour
    {
        //[SerializeField] private SceneLauncher sceneLauncher;
        private GlobalScene globalScene;

        [SerializeField] private Camera camera;

        [SerializeField] private Vector2Int StartPosition;

        [SerializeField] private int SceneLoadRadius = 4;

        public SceneSharedContainer SceneSharedContainer { get; private set; }

        private ISceneFacade sceneFacade;

        private void Awake()
        {
            SceneSharedContainer = Install();

            var cameraPosition = ParcelMathHelper.GetPositionByParcelPosition(StartPosition);
            cameraPosition.y += 8.0f;

            camera.transform.position = cameraPosition;
        }

        private void Start()
        {
            //sceneLauncher.Initialize(SceneSharedContainer, destroyCancellationToken);
            globalScene = new GlobalScene();

            globalScene.Initialize(SceneSharedContainer.SceneFactory, camera, SceneLoadRadius);
        }

        private void OnDestroy()
        {
            globalScene.Dispose();
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
