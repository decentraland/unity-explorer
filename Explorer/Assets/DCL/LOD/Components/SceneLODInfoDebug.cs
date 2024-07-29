using DCL.LOD.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.LOD
{
    public struct SceneLODInfoDebug
       {
        private Dictionary<int, Color[]> OriginalColors;
           private ILODSettingsAsset LodSettingsAsset;

           //This is a sync method, so we can use a shared list
           private static readonly List<Material> TEMP_MATERIALS = new (3);

           private int currentLODCount;

           public static SceneLODInfoDebug Create(ILODSettingsAsset lodSettingsAsset, IReadOnlyList<Vector2Int> parcels)
           {
               var debugCubes =  new DebugCube[parcels.Count];
               for (int i = 0; i < parcels.Count; i++)
               {
                   debugCubes[i] = Object.Instantiate(lodSettingsAsset.DebugCube, ParcelMathHelper.GetPositionByParcelPosition(parcels[i]), Quaternion.identity);
                   debugCubes[i].gameObject.SetActive(false);
               }
               return new SceneLODInfoDebug
               {
                   OriginalColors = new Dictionary<int, Color[]>(), LodSettingsAsset = lodSettingsAsset
               };
           }

           public void Dispose(SceneLODInfo sceneLODInfo)
           {
               //Not initialized
               if (string.IsNullOrEmpty(sceneLODInfo.id))
                   return;

               var lods = sceneLODInfo.metadata.LodGroup.GetLODs();
               for (int lodLevel = 0; lodLevel < lods.Length; lodLevel++)
               {
                   var lodAsset = lods[lodLevel];
                   for (int j = 0; j < lodAsset.renderers.Length; j++)
                   {
                       var lodAssetRenderer = lodAsset.renderers[j];
                       lodAssetRenderer.SafeGetMaterials(TEMP_MATERIALS);
                       foreach (var t in TEMP_MATERIALS)
                       {
                           if (OriginalColors[lodLevel] != null)
                               t.color = OriginalColors[lodLevel][j];
                       }
                   }
               }
           }

           public void Update(SceneLODInfo sceneLODInfo)
           {
               //Not initialized
               if (string.IsNullOrEmpty(sceneLODInfo.id))
                   return;

               //Still no LODs available
               if (currentLODCount == sceneLODInfo.LODLoadedCount())
                   return;

               var lods = sceneLODInfo.metadata.LodGroup.GetLODs();
               for (int lodLevel = 0; lodLevel < lods.Length; lodLevel++)
               {
                   var lodAsset = lods[lodLevel];
                   OriginalColors[lodLevel] = new Color[lodAsset.renderers.Length];
                   for (int j = 0; j < lodAsset.renderers.Length; j++)
                   {
                       var lodAssetRenderer = lodAsset.renderers[j];
                       lodAssetRenderer.SafeGetMaterials(TEMP_MATERIALS);
                       foreach (var t in TEMP_MATERIALS)
                       {
                           OriginalColors[lodLevel][j] = t.color;
                           t.color = LodSettingsAsset.LODDebugColors[lodLevel];
                       }
                   }
               }

               currentLODCount = sceneLODInfo.LODLoadedCount();
           }
       }
       
}
