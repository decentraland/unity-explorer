using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using Arch.System;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;

namespace DCL.LOD.Systems
{

    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class LODDebugToolsSystem : BaseUnityLoopSystem
    {
        private readonly ILODSettingsAsset lodSettingsAsset;
        private static readonly QueryDescription REMOVE_QUERY = new QueryDescription()
            .WithAll<SceneLODInfoDebug, SceneLODInfo>();

        private readonly Material[] failedMaterials;
        private static readonly Shader failedMaterialShader = Shader.Find("Universal Render Pipeline/Lit");
        private bool lodRenderersDisabled;

        public LODDebugToolsSystem(World world, IDebugContainerBuilder debugBuilder, ILODSettingsAsset lodSettingsAsset, int lodLevel) : base(world)
        {
            this.lodSettingsAsset = lodSettingsAsset;
            lodSettingsAsset.IsColorDebugging = false;

            var debugWidgetBuilder = debugBuilder.TryAddWidget("LOD");
            debugWidgetBuilder
                ?.AddSingleButton("LOD Debugging", ToggleLODColor)
                .AddToggleField("Enable LOD Streaming", evt => lodSettingsAsset.EnableLODStreaming = evt.newValue, lodSettingsAsset.EnableLODStreaming);

            for (int i = 0; i < lodSettingsAsset.LodPartitionBucketThresholds.Length; i++)
            {
                int index = i;
                debugWidgetBuilder
                    ?.AddIntFieldWithConfirmation(lodSettingsAsset.LodPartitionBucketThresholds[i], $"LOD {i + 1} Threshold",  newValue => SetLOD(newValue, index));
            }

            debugWidgetBuilder?.AddIntFieldWithConfirmation(lodSettingsAsset.SDK7LodThreshold, "SDK 7 Threshold",  newValue =>
            {
                lodSettingsAsset.SDK7LodThreshold = newValue;
            });

            failedMaterials = new Material[lodLevel];
            for (int i = 0 ; i < lodLevel; i++)
            {
                failedMaterials[i] = new Material(failedMaterialShader)
                {
                    color = lodSettingsAsset.LODDebugColors[i]
                };
            }

            // Subscribe to centralized visual debug settings
            VisualDebugSettings.OnLODRenderersDisabledChanged += OnLODRenderersDisabledFromDebugPanel;
        }

        protected override void OnDispose()
        {
            VisualDebugSettings.OnLODRenderersDisabledChanged -= OnLODRenderersDisabledFromDebugPanel;
        }

        private void OnLODRenderersDisabledFromDebugPanel(bool disabled)
        {
            lodRenderersDisabled = disabled;
            SetLODRenderersVisibilityQuery(World, disabled);
        }

        [Query]
        private void SetLODRenderersVisibility([Data] bool disabled, ref SceneLODInfo sceneLODInfo)
        {
            if (!sceneLODInfo.IsInitialized() || sceneLODInfo.metadata.LodGroup == null)
                return;

            LODGroup lodGroup = sceneLODInfo.metadata.LodGroup;
            UnityEngine.LOD[] lods = lodGroup.GetLODs();

            foreach (UnityEngine.LOD lod in lods)
            {
                if (lod.renderers == null) continue;

                foreach (Renderer renderer in lod.renderers)
                {
                    if (renderer != null)
                        renderer.enabled = !disabled;
                }
            }
        }

        private void SetLOD(int newValue, int i)
        {
            lodSettingsAsset.LodPartitionBucketThresholds[i] = newValue;
        }

        private void ToggleLODColor()
        {
            lodSettingsAsset.IsColorDebugging = !lodSettingsAsset.IsColorDebugging;

            if (!lodSettingsAsset.IsColorDebugging)
                World.Query(REMOVE_QUERY,
                    (Entity entity, ref SceneLODInfoDebug sceneLODInfoDebug, ref SceneLODInfo sceneLODInfo) =>
                    {
                        if (string.IsNullOrEmpty(sceneLODInfo.id))
                            return;

                        sceneLODInfoDebug.Dispose(sceneLODInfo);
                        World.Remove<SceneLODInfoDebug>(entity);
                    });
        }

        protected override void Update(float t)
        {
            if (lodSettingsAsset.IsColorDebugging)
            {
                AddSceneLODInfoDebugQuery(World);
                UpdateLODDebugInfoQuery(World);
                UnloadSceneLODInfoDebugQuery(World);
            }
        }


        [Query]
        [All(typeof(SceneLODInfo))]
        [None(typeof(SceneLODInfoDebug))]
        private void AddSceneLODInfoDebug(in Entity entity)
        {
            World.Add(entity, SceneLODInfoDebug.Create(lodSettingsAsset));
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadSceneLODInfoDebug(in Entity entity, ref SceneLODInfoDebug sceneLODInfoDebug, ref SceneLODInfo sceneLODInfo)
        {
            sceneLODInfoDebug.Dispose(sceneLODInfo);
            World.Remove<SceneLODInfoDebug>(entity);
        }

        [Query]
        private void UpdateLODDebugInfo(ref SceneLODInfo sceneLODInfo, ref SceneLODInfoDebug sceneLODInfoDebug, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            sceneLODInfoDebug.Update(sceneLODInfo, sceneDefinitionComponent.Definition.metadata.scene.DecodedParcels, failedMaterials);
        }
    }

}
