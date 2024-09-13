using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using ECS.SceneLifeCycle.CurrentScene;
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

        private Vector2Int lastParcelProcessed;

        private readonly SceneAssetLock sceneAssetLock;

        private IDebugContainerBuilder debugBuilder;
        private ElementBinding<string> sceneNameBinding;
        private ElementBinding<string> sceneParcelsBinding;
        private ElementBinding<string> sceneHeightBinding;
        private DebugWidgetVisibilityBinding debugInfoVisibilityBinding;
        private bool showDebugCube;
        private GameObject sceneBoundsCube;

        internal UpdateCurrentSceneSystem(World world, IRealmData realmData, IScenesCache scenesCache, CurrentSceneInfo currentSceneInfo,
                                            Entity playerEntity, SceneAssetLock sceneAssetLock, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.currentSceneInfo = currentSceneInfo;
            this.playerEntity = playerEntity;
            this.sceneAssetLock = sceneAssetLock;
            ResetProcessedParcel();

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.CURRENT_SCENE)?
                         .SetVisibilityBinding(debugInfoVisibilityBinding = new DebugWidgetVisibilityBinding(true))
                         .AddCustomMarker("Name:", sceneNameBinding = new ElementBinding<string>(string.Empty))
                         .AddCustomMarker("Parcels:", sceneParcelsBinding = new ElementBinding<string>(string.Empty))
                         .AddCustomMarker("Height (m):", sceneHeightBinding = new ElementBinding<string>(string .Empty))
                         .AddToggleField("Show scene bounds:", (state) => { showDebugCube = state.newValue; }, false);
            this.debugBuilder = debugBuilder;
        }

        private void ResetProcessedParcel()
        {
            lastParcelProcessed = new Vector2Int(int.MinValue, int.MinValue);
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured)
            {
                ResetProcessedParcel();
                return;
            }

            Vector3 playerPos = World.Get<CharacterTransform>(playerEntity).Transform.position;
            Vector2Int parcel = playerPos.ToParcel();
            UpdateSceneReadiness(parcel);
            UpdateCurrentScene(parcel);
            UpdateCurrentSceneInfo(parcel);

            if (debugBuilder.IsVisible && debugInfoVisibilityBinding.IsExpanded)
                RefreshSceneDebugInfo();
        }

        public override void Dispose()
        {
            base.Dispose();

            GameObject.Destroy(sceneBoundsCube);
        }

        private void UpdateSceneReadiness(Vector2Int parcel)
        {

            if (!scenesCache.TryGetByParcel(parcel, out var currentScene))
                return;

            sceneAssetLock.TryLock(currentScene);

            if (!currentScene.SceneStateProvider.IsCurrent)
                currentScene.SetIsCurrent(true);
        }

        private void UpdateCurrentScene(Vector2Int parcel)
        {
            if (lastParcelProcessed == parcel) return;
            scenesCache.TryGetByParcel(lastParcelProcessed, out var lastProcessedScene);
            scenesCache.TryGetByParcel(parcel, out var currentScene);

            if (lastProcessedScene != currentScene)
                lastProcessedScene?.SetIsCurrent(false);

            if (currentScene is { SceneStateProvider: { IsCurrent: false } })
                currentScene.SetIsCurrent(true);

            lastParcelProcessed = parcel;
            scenesCache.SetCurrentScene(currentScene);
        }

        private void UpdateCurrentSceneInfo(Vector2Int parcel)
        {
            scenesCache.TryGetByParcel(parcel, out var currentScene);
            currentSceneInfo.Update(currentScene);
            scenesCache.SetCurrentScene(currentScene);
        }

        private void RefreshSceneDebugInfo()
        {
            if (scenesCache.CurrentScene != null)
            {
                sceneBoundsCube?.SetActive(showDebugCube);

                if (sceneNameBinding.Value != scenesCache.CurrentScene.Info.Name)
                {
                    sceneNameBinding.Value = scenesCache.CurrentScene.Info.Name;

                    if (scenesCache.CurrentScene.SceneData.Parcels != null)
                    {
                        sceneParcelsBinding.Value = scenesCache.CurrentScene.SceneData.Parcels.Count.ToString();
                    }

                    sceneHeightBinding.Value = scenesCache.CurrentScene.SceneData.Geometry.Height.ToString();

                    if (sceneBoundsCube == null)
                    {
                        sceneBoundsCube = CreateDebugCube();
                    }

                    UpdateDebugCube(scenesCache.CurrentScene.SceneData.Geometry, sceneBoundsCube);
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
