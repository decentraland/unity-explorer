﻿using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AssetsProvision;
using DCL.AsyncLoadReporting;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.SceneBoundsChecker;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
    ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UpdateSceneLODInfoSystem : BaseUnityLoopSystem
    {
        private readonly ILODAssetsPool lodCache;
        private readonly ILODSettingsAsset lodSettingsAsset;
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IScenesCache scenesCache;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;

        private readonly Transform lodsTransformParent;

        private static readonly Shader shader = Shader.Find("DCL/Avatar_CelShading");
        private readonly Dictionary<TextureFormat, TextureArrayContainer> textureArrayContainerDictionary;

        public UpdateSceneLODInfoSystem(World world, ILODAssetsPool lodCache, ILODSettingsAsset lodSettingsAsset,
            IPerformanceBudget memoryBudget, IPerformanceBudget frameCapBudget, IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue, Transform lodsTransformParent) : base(world)
        {
            this.lodCache = lodCache;
            this.lodSettingsAsset = lodSettingsAsset;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.lodsTransformParent = lodsTransformParent;
            textureArrayContainerDictionary = new Dictionary<TextureFormat, TextureArrayContainer>
            {
                {
                    TextureFormat.BC7, new TextureArrayContainer(TextureFormat.BC7)
                },
                {
                    TextureFormat.DXT1, new TextureArrayContainer(TextureFormat.DXT1)
                }
            };
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
            ResolveCurrentLODPromiseQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (partitionComponent.IsDirty || sceneLODInfo.CurrentLODLevel == byte.MaxValue)
                CheckLODLevel(ref partitionComponent, ref sceneLODInfo, sceneDefinitionComponent);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ResolveCurrentLODPromise(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!sceneLODInfo.IsDirty) return;

            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) return;

            if (sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                sceneLODInfo.CurrentLOD?.Release();

                if (result.Succeeded)
                {
                    GameObject? instantiatedLOD = Object.Instantiate(result.Asset.GameObject, sceneDefinitionComponent.SceneGeometry.BaseParcelPosition,
                        Quaternion.identity, lodsTransformParent);
                    //ConfigureSceneMaterial.EnableSceneBounds(in instantiatedLOD,
                    //    in sceneDefinitionComponent.SceneGeometry.CircumscribedPlanes);

                    if (!sceneLODInfo.CurrentLODLevel.Equals(0))
                    {
                        var componentsInChildren = instantiatedLOD.GetComponentsInChildren<MeshRenderer>();
                        for (int i = 0; i < componentsInChildren.Length; i++)
                        {
                            var newMaterials =  new List<Material>();
                            for (int j = 0; j < componentsInChildren[i].materials.Length; j++)
                            {
                                var newMaterial = new Material(shader);
                                if (componentsInChildren[i].materials[j].mainTexture != null)
                                {
                                    switch (componentsInChildren[i].materials[j].mainTexture.graphicsFormat)
                                    {
                                        case GraphicsFormat.RGBA_BC7_UNorm:
                                        case GraphicsFormat.RGBA_BC7_SRGB:
                                            textureArrayContainerDictionary[TextureFormat.BC7].SetTexture(newMaterial, (Texture2D)componentsInChildren[i].materials[j].mainTexture, ComputeShaderConstants.TextureArrayType.ALBEDO);
                                            break;
                                        case GraphicsFormat.RGBA_DXT1_UNorm:
                                        case GraphicsFormat.RGBA_DXT1_SRGB:
                                            textureArrayContainerDictionary[TextureFormat.DXT1].SetTexture(newMaterial, (Texture2D)componentsInChildren[i].materials[j].mainTexture, ComputeShaderConstants.TextureArrayType.ALBEDO);
                                            break;
                                    }
                                }

                                newMaterials.Add(newMaterial);
                            }

                            componentsInChildren[i].materials = newMaterials.ToArray();
                        }
                    }
                    
                    sceneLODInfo.CurrentLOD = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel),
                        instantiatedLOD, result.Asset, lodCache);
                }
                else
                {
                    ReportHub.LogWarning(GetReportCategory(),
                        $"LOD request for {sceneLODInfo.CurrentLODPromise.LoadingIntention.Hash} failed");

                    sceneLODInfo.CurrentLOD = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel));
                }

                scenesCache.Add(sceneLODInfo, sceneDefinitionComponent.Parcels);

                CheckSceneReadiness(sceneDefinitionComponent);

                sceneLODInfo.IsDirty = false;
            }
        }

        private void CheckLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            //If we are in an SDK6 scene, this value will be kept.
            //Therefore, lod0 will be shown
            byte sceneLODCandidate = 0;

            for (byte i = 0; i < lodSettingsAsset.LodPartitionBucketThresholds.Length; i++)
            {
                if (partitionComponent.Bucket >= lodSettingsAsset.LodPartitionBucketThresholds[i])
                    sceneLODCandidate = (byte)(i + 1);
            }

            if (sceneLODCandidate != sceneLODInfo.CurrentLODLevel)
                UpdateLODLevel(ref partitionComponent, ref sceneLODInfo, sceneLODCandidate, sceneDefinitionComponent);
        }

        private void UpdateLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo,
            byte sceneLODCandidate, SceneDefinitionComponent sceneDefinitionComponent)
        {
            sceneLODInfo.CurrentLODPromise.ForgetLoading(World);
            sceneLODInfo.CurrentLODLevel = sceneLODCandidate;
            var newLODKey = new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel);

            //If the current LOD is the candidate, no need to make a new promise or set anything new
            if (newLODKey.Equals(sceneLODInfo.CurrentLOD))
            {
                sceneLODInfo.IsDirty = false;
                return;
            }

            if (lodCache.TryGet(newLODKey, out var cachedAsset))
            {
                //If its cached, no need to make a new promise
                sceneLODInfo.CurrentLOD?.Release();
                sceneLODInfo.CurrentLOD = cachedAsset;
                sceneLODInfo.IsDirty = false;
                CheckSceneReadiness(sceneDefinitionComponent);
                sceneLODInfo.CurrentLOD.Value.Root.transform.SetParent(lodsTransformParent);
            }
            else
            {
                //TODO: TEMP, for some reason genesis plaza asset is crashing in mac
                if ((Application.platform.Equals(RuntimePlatform.OSXPlayer) ||
                     Application.platform.Equals(RuntimePlatform.OSXEditor)) &&
                    sceneDefinitionComponent.Definition.id.Equals("bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                {
                    return;
                }

                //TODO: TEMP, infinite floor sceene
                if (sceneDefinitionComponent.Definition.id.Equals("bafkreictb7lsedstowe2twuqjk7nn3hvqs3s2jqhc2bduwmein73xxelbu"))
                {
                    return;
                }

                sceneLODInfo.CurrentLODPromise =
                    Promise.Create(World,
                        GetAssetBundleIntention.FromHash(newLODKey.ToString(),
                            permittedSources: AssetSource.EMBEDDED,
                            customEmbeddedSubDirectory: URLSubdirectory.FromString("lods")),
                        partitionComponent);

                sceneLODInfo.IsDirty = true;
            }
        }

        private void CheckSceneReadiness(SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneReadinessReportQueue.TryDequeue(sceneDefinitionComponent.Parcels, out var reports))
            {
                for (int i = 0; i < reports!.Value.Count; i++)
                {
                    var report = reports.Value[i];
                    report.ProgressCounter.Value = 1f;
                    report.CompletionSource.TrySetResult();
                }
            }
        }
    }
}
