using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Utility;

namespace Global
{
    /// <summary>
    ///     An entry point to install and resolve all dependencies
    /// </summary>
    public class DynamicSceneLoader : MonoBehaviour
    {
        [SerializeField] private Camera camera;

        [SerializeField] private Vector2Int StartPosition;

        [SerializeField] private int SceneLoadRadius = 4;

        // If it's 0, it will load every parcel in the range
        [SerializeField] private List<Vector2Int> StaticLoadPositions;
        private GlobalWorld globalWorld;

        private ISceneFacade sceneFacade;

        public SceneSharedContainer SceneSharedContainer { get; private set; }

        private void Awake()
        {
            SceneSharedContainer = Install();

            Vector3 cameraPosition = ParcelMathHelper.GetPositionByParcelPosition(StartPosition);
            cameraPosition.y += 8.0f;

            camera.transform.position = cameraPosition;
        }

        private void Start()
        {
            globalWorld = new GlobalWorld();

            List<Vector2Int> staticLoadPositions = StaticLoadPositions.Count > 0 ? StaticLoadPositions : null;

            globalWorld.Initialize(SceneSharedContainer.SceneFactory, camera, SceneLoadRadius, staticLoadPositions);
            globalWorld.SetRealm("https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main");
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
