using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using ECS.SceneLifeCycle.CurrentScene;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Detects the scene the player is currently in
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class UpdateCurrentSceneSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;
        private readonly CurrentSceneInfo currentSceneInfo;

        private readonly IDebugContainerBuilder debugBuilder;
        private readonly ElementBinding<string> sceneNameBinding;
        private readonly ElementBinding<string> sceneParcelsBinding;
        private readonly ElementBinding<string> sceneHeightBinding;
        private readonly ElementBinding<string> sdk6Binding;
        private readonly DebugWidgetVisibilityBinding debugInfoVisibilityBinding;
        private bool showDebugCube;
        private GameObject sceneBoundsCube;
        private ISceneFacade? currentActiveScene;
        private Vector2Int previousParcelPosition;

        internal UpdateCurrentSceneSystem(World world, IRealmData realmData, IScenesCache scenesCache, CurrentSceneInfo currentSceneInfo,
            Entity playerEntity, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.currentSceneInfo = currentSceneInfo;
            this.playerEntity = playerEntity;

            debugInfoVisibilityBinding = new DebugWidgetVisibilityBinding(true);
            sdk6Binding = new ElementBinding<string>(string.Empty);
            sceneNameBinding = new ElementBinding<string>(string.Empty);
            sceneParcelsBinding = new ElementBinding<string>(string.Empty);
            sceneHeightBinding = new ElementBinding<string>(string.Empty);

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.CURRENT_SCENE)?
                         .SetVisibilityBinding(debugInfoVisibilityBinding)
                         .AddCustomMarker("SDK 6:", sdk6Binding)
                         .AddCustomMarker("Name:", sceneNameBinding)
                         .AddCustomMarker("Parcels:", sceneParcelsBinding)
                         .AddCustomMarker("Height (m):", sceneHeightBinding)
                         .AddToggleField("Show scene bounds:", state => { showDebugCube = state.newValue; }, false);
            this.debugBuilder = debugBuilder;
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured)
                return;

            Vector3 playerPos = World.Get<CharacterTransform>(playerEntity).Transform.position;
            Vector2Int parcel = playerPos.ToParcel();
            UpdateSceneReadiness(parcel);

            if (debugBuilder.IsVisible && debugInfoVisibilityBinding.IsConnectedAndExpanded)
                RefreshSceneDebugInfo();
        }

        private void UpdateSceneReadiness(Vector2Int parcel)
        {
            if (scenesCache.TryGetByParcel(parcel, out var currentScene))
            {
                if (currentActiveScene != currentScene)
                {
                    currentActiveScene?.SetIsCurrent(false);
                    currentActiveScene = currentScene;
                    currentActiveScene.SetIsCurrent(true);
                    UpdateCurrentScene();
                }
            }
            else
            {
                if(currentActiveScene != null)
                {
                    currentActiveScene.SetIsCurrent(false);
                    currentActiveScene = null;
                    UpdateCurrentScene();
                }
            }
        }

        private void UpdateCurrentScene()
        {
            currentSceneInfo.Update(currentActiveScene);
            scenesCache.SetCurrentScene(currentActiveScene);
        }

        protected override void OnDispose()
        {
            GameObject.Destroy(sceneBoundsCube);
        }

        private void RefreshSceneDebugInfo()
        {
            sdk6Binding.Value = currentActiveScene != null ? bool.FalseString : bool.TrueString;

            if (currentActiveScene != null)
            {
                sceneBoundsCube?.SetActive(showDebugCube);

                if (sceneNameBinding.Value != currentActiveScene.Info.Name)
                {
                    sceneNameBinding.Value = currentActiveScene.Info.Name;

                    if (currentActiveScene.SceneData.Parcels != null)
                    {
                        sceneParcelsBinding.Value = currentActiveScene.SceneData.Parcels.Count.ToString();
                    }

                    sceneHeightBinding.Value = currentActiveScene.SceneData.Geometry.Height.ToString();

                    if (sceneBoundsCube == null)
                    {
                        sceneBoundsCube = CreateDebugCube();
                    }

                    UpdateDebugCube(currentActiveScene.SceneData.Geometry, sceneBoundsCube);
                }
            }
            else
            {
                sceneNameBinding.Value = "<No data>";
                sceneParcelsBinding.Value = "<No data>";
                sceneHeightBinding.Value = "<No data>";
                sceneBoundsCube?.SetActive(false);
            }
        }

        private static GameObject CreateDebugCube()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "DebugSceneBoundsCube";

            Material cubeMaterial = new Material(Shader.Find("DCL/Scene"));
            cubeMaterial.color = new Color(1.0f, 0.0f, 0.0f, 0.8f);
            cubeMaterial.SetFloat("_SrcBlend", (int)BlendMode.One);
            cubeMaterial.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            cubeMaterial.SetFloat("_Cull", (int)CullMode.Off);
            cubeMaterial.SetFloat("_Surface", 1.0f); // 1 means transparent
            cubeMaterial.renderQueue = (int)RenderQueue.Transparent;
            cube.GetComponent<MeshRenderer>().material = cubeMaterial;

            GameObject.Destroy(cube.GetComponent<Collider>());

            cube.SetActive(false);
            return cube;
        }

        private static void UpdateDebugCube(ParcelMathHelper.SceneGeometry sceneGeometry, GameObject cube)
        {
            // Makes the cube fit the scene bounds
            Vector3 cubeSize = new Vector3(sceneGeometry.CircumscribedPlanes.MaxX - sceneGeometry.CircumscribedPlanes.MinX,
                                           sceneGeometry.Height,
                                           sceneGeometry.CircumscribedPlanes.MaxZ - sceneGeometry.CircumscribedPlanes.MinZ);
            cube.transform.position = new Vector3(sceneGeometry.CircumscribedPlanes.MinX + cubeSize.x * 0.5f,
                                                  cubeSize.y * 0.5f,
                                                  sceneGeometry.CircumscribedPlanes.MinZ + cubeSize.z * 0.5f);
            cube.transform.localScale = cubeSize;
        }
    }
}
