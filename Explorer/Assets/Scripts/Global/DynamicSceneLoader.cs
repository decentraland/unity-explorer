using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;
using Utility;

namespace Global
{
    /// <summary>
    /// An entry point to install and resolve all dependencies
    /// </summary>
    public class DynamicSceneLoader : MonoBehaviour
    {
        private GlobalWorld globalWorld;

        [SerializeField] private Camera camera;

        [SerializeField] private Vector2Int StartPosition;

        [SerializeField] private int SceneLoadRadius = 4;

        // If it's 0, it will load every parcel in the range
        [SerializeField] private List<Vector2Int> StaticLoadPositions;

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
            globalWorld = new GlobalWorld();

            var staticLoadPositions = StaticLoadPositions.Count > 0 ? StaticLoadPositions : null;

            globalWorld.Initialize(SceneSharedContainer.SceneFactory, camera, SceneLoadRadius, staticLoadPositions);
        }

        private void OnDestroy()
        {
            globalWorld.Dispose();
        }

        public static SceneSharedContainer Install()
        {
            Profiler.BeginSample($"{nameof(DynamicSceneLoader)}.Install");

            var componentsContainer = ComponentsContainer.Create();
            var sceneSharedContainer = SceneSharedContainer.Create(componentsContainer);

            Profiler.EndSample();
            return sceneSharedContainer;
        }
    }
}
