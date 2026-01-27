using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using ECS.SceneLifeCycle.CurrentScene;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Globalization;
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
        private const string NO_DATA_STRING = "<No data>";

        private static readonly int SRC_BLEND = Shader.PropertyToID("_SrcBlend");
        private static readonly int DST_BLEND = Shader.PropertyToID("_DstBlend");
        private static readonly int CULL = Shader.PropertyToID("_Cull");
        private static readonly int SURFACE = Shader.PropertyToID("_Surface");

        private readonly Entity playerEntity;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;
        private readonly CurrentSceneInfo currentSceneInfo;

        private readonly IDebugContainerBuilder debugBuilder;
        private readonly ElementBinding<string> sceneNameBinding;
        private readonly ElementBinding<string> sceneParcelsBinding;
        private readonly ElementBinding<string> sceneHeightBinding;
        private readonly ElementBinding<string> sdk6Binding;
        private readonly ElementBinding<string> globalPositionBinding;
        private readonly ElementBinding<string> sceneRelativePositionBinding;
        private readonly DebugWidgetVisibilityBinding debugInfoVisibilityBinding;
        private bool showDebugCube;
        private bool sceneVisible = true;
        private bool backfaceCulling;
        private bool shadowsDisabled;
        private Light? directionalLight;
        private LightShadows? originalShadowType;
        private readonly Dictionary<Material, int> originalCullValues = new ();
        private readonly Dictionary<Renderer, ShadowCastingMode> originalShadowModes = new ();
        private GameObject? sceneBoundsCube;
        private ISceneFacade? currentActiveScene;
        private Vector2Int previousParcelPosition;

        private Vector2Int lastParcel;

        internal UpdateCurrentSceneSystem(
            World world,
            IRealmData realmData,
            IScenesCache scenesCache,
            CurrentSceneInfo currentSceneInfo,
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
            globalPositionBinding = new ElementBinding<string>(string.Empty);
            sceneRelativePositionBinding = new ElementBinding<string>(string.Empty);

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.CURRENT_SCENE)?
                         .SetVisibilityBinding(debugInfoVisibilityBinding)
                         .AddCustomMarker("SDK 6:", sdk6Binding)
                         .AddCustomMarker("Name:", sceneNameBinding)
                         .AddCustomMarker("Parcels:", sceneParcelsBinding)
                         .AddCustomMarker("Height (m):", sceneHeightBinding)
                         .AddCustomMarker("Global Pos:", globalPositionBinding)
                         .AddCustomMarker("Scene Pos:", sceneRelativePositionBinding)
                         .AddToggleField("Show scene bounds:", state => { showDebugCube = state.newValue; }, false)
                         .AddToggleField("Scene Visible:", OnSceneVisibleToggle, sceneVisible)
                         .AddToggleField("Backface Culling:", OnBackfaceCullingToggle, backfaceCulling)
                         .AddIntFieldWithConfirmation(-1, "Limit Shadow Casters", OnLimitShadowCasters)
                         .AddSingleButton("Restore Shadow Casters", OnRestoreShadowCasters)
                         .AddToggleField("Disable Shadows:", OnDisableShadowsToggle, shadowsDisabled);
            this.debugBuilder = debugBuilder;

            // Subscribe to centralized visual debug settings
            VisualDebugSettings.OnSceneRendererVisibleChanged += OnSceneRendererVisibleFromDebugPanel;
            VisualDebugSettings.OnBackfaceCullingChanged += OnBackfaceCullingFromDebugPanel;
            VisualDebugSettings.OnShadowLimiterChanged += OnShadowLimiterFromDebugPanel;
            VisualDebugSettings.OnShadowsDisabledChanged += OnShadowsDisabledFromDebugPanel;
        }

        private void OnSceneRendererVisibleFromDebugPanel(bool visible)
        {
            sceneVisible = visible;
            ApplySceneVisibility();
        }

        private void OnBackfaceCullingFromDebugPanel(bool enabled)
        {
            backfaceCulling = enabled;
            ApplyBackfaceCulling();
        }

        private void OnShadowLimiterFromDebugPanel(int maxShadowCasters)
        {
            OnLimitShadowCasters(maxShadowCasters);
        }

        private void OnShadowsDisabledFromDebugPanel(bool disabled)
        {
            shadowsDisabled = disabled;
            ApplyShadowsDisabled();
        }

        private void ApplyShadowsDisabled()
        {
            // Find the main directional light
            if (directionalLight == null)
            {
                GameObject lightObject = GameObject.Find("Directional Light");
                if (lightObject != null)
                    directionalLight = lightObject.GetComponent<Light>();
            }

            if (directionalLight != null)
            {
                if (shadowsDisabled)
                {
                    if (!originalShadowType.HasValue)
                        originalShadowType = directionalLight.shadows;

                    directionalLight.shadows = LightShadows.None;
                }
                else
                {
                    if (originalShadowType.HasValue)
                        directionalLight.shadows = originalShadowType.Value;
                }
            }
        }

        private void ApplySceneVisibility()
        {
            if (currentActiveScene == null)
                return;

            Entity sceneContainer = currentActiveScene.PersistentEntities.SceneContainer;
            World sceneWorld = currentActiveScene.EcsExecutor.World;

            if (sceneWorld.Has<TransformComponent>(sceneContainer))
            {
                ref TransformComponent transformComponent = ref sceneWorld.Get<TransformComponent>(sceneContainer);
                Renderer[] renderers = transformComponent.Transform.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer renderer in renderers)
                    renderer.enabled = sceneVisible;
            }
        }

        private void ApplyBackfaceCulling()
        {
            if (currentActiveScene == null)
                return;

            Entity sceneContainer = currentActiveScene.PersistentEntities.SceneContainer;
            World sceneWorld = currentActiveScene.EcsExecutor.World;

            if (sceneWorld.Has<TransformComponent>(sceneContainer))
            {
                ref TransformComponent transformComponent = ref sceneWorld.Get<TransformComponent>(sceneContainer);
                Renderer[] renderers = transformComponent.Transform.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer renderer in renderers)
                {
                    foreach (Material material in renderer.materials)
                    {
                        if (material == null || !material.HasProperty(CULL))
                            continue;

                        if (backfaceCulling)
                        {
                            if (!originalCullValues.ContainsKey(material))
                                originalCullValues[material] = material.GetInt(CULL);

                            material.SetInt(CULL, (int)CullMode.Back);
                        }
                        else
                        {
                            if (originalCullValues.TryGetValue(material, out int originalValue))
                                material.SetInt(CULL, originalValue);
                        }
                    }
                }

                if (!backfaceCulling)
                    originalCullValues.Clear();
            }
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured)
            {
                lastParcel = new Vector2Int(int.MinValue, int.MinValue);
                return;
            }
            Vector2Int parcel = World.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();

            lastParcel = parcel;
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
                if (currentActiveScene != null)
                {
                    currentActiveScene.SetIsCurrent(false);
                    currentActiveScene = null;
                    UpdateCurrentScene();
                }
            }

            scenesCache.UpdateCurrentParcel(parcel);
        }

        private void UpdateCurrentScene()
        {
            currentSceneInfo.Update(currentActiveScene);
            scenesCache.SetCurrentScene(currentActiveScene);
        }

        private void OnSceneVisibleToggle(UnityEngine.UIElements.ChangeEvent<bool> evt)
        {
            sceneVisible = evt.newValue;

            if (currentActiveScene == null)
                return;

            // Get the scene container transform and toggle all renderers
            Entity sceneContainer = currentActiveScene.PersistentEntities.SceneContainer;
            World sceneWorld = currentActiveScene.EcsExecutor.World;

            if (sceneWorld.Has<TransformComponent>(sceneContainer))
            {
                ref TransformComponent transformComponent = ref sceneWorld.Get<TransformComponent>(sceneContainer);
                Renderer[] renderers = transformComponent.Transform.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer renderer in renderers)
                    renderer.enabled = sceneVisible;
            }
        }

        private void OnBackfaceCullingToggle(UnityEngine.UIElements.ChangeEvent<bool> evt)
        {
            backfaceCulling = evt.newValue;

            if (currentActiveScene == null)
                return;

            Entity sceneContainer = currentActiveScene.PersistentEntities.SceneContainer;
            World sceneWorld = currentActiveScene.EcsExecutor.World;

            if (sceneWorld.Has<TransformComponent>(sceneContainer))
            {
                ref TransformComponent transformComponent = ref sceneWorld.Get<TransformComponent>(sceneContainer);
                Renderer[] renderers = transformComponent.Transform.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer renderer in renderers)
                {
                    foreach (Material material in renderer.materials)
                    {
                        if (material == null || !material.HasProperty(CULL))
                            continue;

                        if (backfaceCulling)
                        {
                            // Store original value and set to Front (cull front faces, show back faces)
                            if (!originalCullValues.ContainsKey(material))
                                originalCullValues[material] = material.GetInt(CULL);

                            material.SetInt(CULL, (int)CullMode.Back);
                        }
                        else
                        {
                            // Restore original value
                            if (originalCullValues.TryGetValue(material, out int originalValue))
                                material.SetInt(CULL, originalValue);
                        }
                    }
                }

                if (!backfaceCulling)
                    originalCullValues.Clear();
            }
        }

        private void OnLimitShadowCasters(int maxShadowCasters)
        {
            if (currentActiveScene == null)
                return;

            Entity sceneContainer = currentActiveScene.PersistentEntities.SceneContainer;
            World sceneWorld = currentActiveScene.EcsExecutor.World;

            if (sceneWorld.Has<TransformComponent>(sceneContainer))
            {
                ref TransformComponent transformComponent = ref sceneWorld.Get<TransformComponent>(sceneContainer);
                Renderer[] renderers = transformComponent.Transform.GetComponentsInChildren<Renderer>(true);

                int shadowCasterCount = 0;

                foreach (Renderer renderer in renderers)
                {
                    if (renderer.shadowCastingMode == ShadowCastingMode.Off)
                        continue;

                    // Store original value if not already stored
                    if (!originalShadowModes.ContainsKey(renderer))
                        originalShadowModes[renderer] = renderer.shadowCastingMode;

                    if (maxShadowCasters < 0 || shadowCasterCount < maxShadowCasters)
                    {
                        // Keep shadow casting enabled (restore if previously disabled)
                        renderer.shadowCastingMode = originalShadowModes[renderer];
                        shadowCasterCount++;
                    }
                    else
                    {
                        // Disable shadow casting
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }
        }

        private void OnRestoreShadowCasters()
        {
            if (currentActiveScene == null)
                return;

            Entity sceneContainer = currentActiveScene.PersistentEntities.SceneContainer;
            World sceneWorld = currentActiveScene.EcsExecutor.World;

            if (sceneWorld.Has<TransformComponent>(sceneContainer))
            {
                ref TransformComponent transformComponent = ref sceneWorld.Get<TransformComponent>(sceneContainer);
                Renderer[] renderers = transformComponent.Transform.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer renderer in renderers)
                {
                    if (originalShadowModes.TryGetValue(renderer, out ShadowCastingMode originalMode))
                        renderer.shadowCastingMode = originalMode;
                }

                originalShadowModes.Clear();
            }
        }

        private void OnDisableShadowsToggle(UnityEngine.UIElements.ChangeEvent<bool> evt)
        {
            shadowsDisabled = evt.newValue;

            // Find the main directional light
            if (directionalLight == null)
            {
                GameObject lightObject = GameObject.Find("Directional Light");
                if (lightObject != null)
                {
                    directionalLight = lightObject.GetComponent<Light>();
                }
            }

            if (directionalLight != null)
            {
                if (shadowsDisabled)
                {
                    // Store original shadow type and disable shadows
                    if (!originalShadowType.HasValue)
                        originalShadowType = directionalLight.shadows;

                    directionalLight.shadows = LightShadows.None;
                }
                else
                {
                    // Restore original shadow type
                    if (originalShadowType.HasValue)
                        directionalLight.shadows = originalShadowType.Value;
                }
            }
        }

        protected override void OnDispose()
        {
            Object.Destroy(sceneBoundsCube);

            // Unsubscribe from centralized visual debug settings
            VisualDebugSettings.OnSceneRendererVisibleChanged -= OnSceneRendererVisibleFromDebugPanel;
            VisualDebugSettings.OnBackfaceCullingChanged -= OnBackfaceCullingFromDebugPanel;
            VisualDebugSettings.OnShadowLimiterChanged -= OnShadowLimiterFromDebugPanel;
            VisualDebugSettings.OnShadowsDisabledChanged -= OnShadowsDisabledFromDebugPanel;
        }

        private void RefreshSceneDebugInfo()
        {
            sdk6Binding.Value = currentActiveScene != null ? bool.FalseString : bool.TrueString;

            Vector3 globalPosition = World.Get<CharacterTransform>(playerEntity).Transform.position;
            globalPositionBinding.Value = FormatPositionVector(globalPosition);

            if (currentActiveScene != null)
            {
                sceneBoundsCube?.SetActive(showDebugCube);

                Vector3 sceneBasePosition = currentActiveScene.SceneData.Geometry.BaseParcelPosition;
                Vector3 sceneRelativePosition = globalPosition.FromGlobalToSceneRelativePosition(sceneBasePosition);
                sceneRelativePositionBinding.Value = FormatPositionVector(sceneRelativePosition);

                if (sceneNameBinding.Value != currentActiveScene.Info.Name)
                {
                    sceneNameBinding.Value = currentActiveScene.Info.Name;

                    if (currentActiveScene.SceneData.Parcels != null)
                    {
                        sceneParcelsBinding.Value = currentActiveScene.SceneData.Parcels.Count.ToString();
                    }

                    sceneHeightBinding.Value = currentActiveScene.SceneData.Geometry.Height.ToString(CultureInfo.InvariantCulture);

                    if (sceneBoundsCube == null)
                    {
                        sceneBoundsCube = CreateDebugCube();
                    }

                    UpdateDebugCube(currentActiveScene.SceneData.Geometry, sceneBoundsCube);
                }
            }
            else
            {
                sceneNameBinding.Value = NO_DATA_STRING;
                sceneParcelsBinding.Value = NO_DATA_STRING;
                sceneHeightBinding.Value = NO_DATA_STRING;
                sceneRelativePositionBinding.Value = NO_DATA_STRING;
                sceneBoundsCube?.SetActive(false);
            }
        }

        private static string FormatPositionVector(Vector3 position) =>
            $"{position.x:F1}, {position.y:F1}, {position.z:F1}";

        private static GameObject CreateDebugCube()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "DebugSceneBoundsCube";

            Material cubeMaterial = new Material(Shader.Find("DCL/Scene"))
                {
                    color = new (1.0f, 0.0f, 0.0f, 0.8f),
                };

            cubeMaterial.SetFloat(SRC_BLEND, (int)BlendMode.One);
            cubeMaterial.SetFloat(DST_BLEND, (int)BlendMode.OneMinusSrcAlpha);
            cubeMaterial.SetFloat(CULL, (int)CullMode.Off);
            cubeMaterial.SetFloat(SURFACE, 1.0f); // 1.0f means it's transparent
            cubeMaterial.renderQueue = (int)RenderQueue.Transparent;
            cube.GetComponent<MeshRenderer>().material = cubeMaterial;

            Object.Destroy(cube.GetComponent<Collider>());

            cube.SetActive(false);
            return cube;
        }

        private static void UpdateDebugCube(ParcelMathHelper.SceneGeometry sceneGeometry, GameObject cube)
        {
            // Makes the cube fit the scene bounds
            Vector3 cubeSize = new Vector3(sceneGeometry.CircumscribedPlanes.MaxX - sceneGeometry.CircumscribedPlanes.MinX,
                                           sceneGeometry.Height,
                                           sceneGeometry.CircumscribedPlanes.MaxZ - sceneGeometry.CircumscribedPlanes.MinZ);
            cube.transform.position = new Vector3(sceneGeometry.CircumscribedPlanes.MinX + (cubeSize.x * 0.5f),
                                                  cubeSize.y * 0.5f,
                                                  sceneGeometry.CircumscribedPlanes.MinZ + (cubeSize.z * 0.5f));
            cube.transform.localScale = cubeSize;
        }
    }
}
