﻿using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;
using DCL.LOD.Components;
using ECS.SceneLifeCycle;
using UnityEngine;
using Utility;

namespace DCL.LOD.Systems
{
    public static class LODUtils
    {
        public static readonly URLSubdirectory LOD_EMBEDDED_SUBDIRECTORIES = URLSubdirectory.FromString("lods");

        private static readonly ListObjectPool<TextureArraySlot?> TEXTURE_ARRAY_SLOTS = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);
        private static string LOD_SHADER = "DCL/Scene_TexArray";
        private static readonly List<Material> TEMP_MATERIALS = new (3);

        public static TextureArraySlot?[] ApplyTextureArrayToLOD(string sceneID, Vector2Int baseCoordinate, GameObject instantiatedLOD, TextureArrayContainer lodTextureArrayContainer)
        {
            var newSlots = TEXTURE_ARRAY_SLOTS.Get();

            using (var pooledList = instantiatedLOD.GetComponentsInChildrenIntoPooledList<Renderer>(true))
            {
                for (int i = 0; i < pooledList.Value.Count; i++)
                {
                    pooledList.Value[i].SafeGetMaterials(TEMP_MATERIALS);

                    for (int j = 0; j < TEMP_MATERIALS.Count; j++)
                    {
                        if (TEMP_MATERIALS[j].mainTexture != null)
                        {
                            if (TEMP_MATERIALS[j].mainTexture.width != TEMP_MATERIALS[j].mainTexture.height)
                            {
                                ReportHub.LogWarning(ReportCategory.LOD, $"Trying to apply a non square resolution in {sceneID} {baseCoordinate}");
                                continue;
                            }

                            if (TEMP_MATERIALS[j].shader.name != LOD_SHADER)
                            {
                                ReportHub.LogWarning(ReportCategory.LOD, $"One material does not have the correct shader in {sceneID} {baseCoordinate}. " +
                                                                         $"It has {pooledList.Value[i].materials[j].shader} while it should be {LOD_SHADER}. Please check the AB Converter");

                                continue;
                            }

                            newSlots.AddRange(lodTextureArrayContainer.SetTexturesFromOriginalMaterial(pooledList.Value[i].materials[j], pooledList.Value[i].materials[j]));
                            TEMP_MATERIALS[j].mainTexture = null;
                        }
                    }
                }
            }

            return newSlots.ToArray();
        }

        public static void TryReportSDK6SceneLoadedForLOD(SceneLODInfo sceneLODInfo,
            SceneDefinitionComponent sceneDefinitionComponent, ISceneReadinessReportQueue sceneReadinessReportQueue,
            IScenesCache scenesCache)
        {
            //We have to report ready scenes for LOD_0 which are not SDK7. Only then we can consider this scenes as loaded
            if (!sceneDefinitionComponent.IsSDK7 && sceneLODInfo.HasLOD(0))
                SceneUtils.ReportSceneLoaded(sceneDefinitionComponent, sceneReadinessReportQueue, scenesCache);
        }
    }
}
