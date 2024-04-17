using DCL.Landscape.Settings;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Landscape
{
    public class TerrainFactory
    {
        private readonly TerrainGenerationData terrainGenData;

        private TreePrototype[] treePrototypes;

        public TerrainFactory(TerrainGenerationData terrainGenData)
        {
            this.terrainGenData = terrainGenData;
        }

        public static GameObject InstantiateSingletonTerrainRoot(string terrainObjectName)
        {
            var rootGo = GameObject.Find(terrainObjectName);

            if (rootGo != null)
                UnityObjectUtils.SafeDestroy(rootGo);

            rootGo = new GameObject(terrainObjectName);
            return rootGo;
        }

        public Transform CreateOcean(Transform parent, bool worldPositionStays = true) =>
            Object.Instantiate(terrainGenData.ocean, parent, worldPositionStays).transform;

        public Transform CreateWind() =>
            Object.Instantiate(terrainGenData.wind).transform;

        public static Transform CreateCliffsRoot(Transform parent)
        {
            Transform cliffsRoot = new GameObject("Cliffs").transform;
            cliffsRoot.SetParent(parent);

            return cliffsRoot;
        }

        public void CreateCliffCorner(Transform parent, Vector3 at, Quaternion rotation)
        {
            Transform neCorner = Object.Instantiate(terrainGenData.cliffCorner, at, rotation).transform;
            neCorner.SetParent(parent, true);
        }

        public Transform CreateCliffSide(Transform parent, Vector3 at, Quaternion rotation)
        {
            Transform side = Object.Instantiate(terrainGenData.cliffSide, at, rotation).transform;
            side.SetParent(parent, true);

            return side;
        }

        public static Transform CreateCollidersRoot(Transform parent)
        {
            var collidersRoot = new GameObject("BorderColliders").transform;
            collidersRoot.SetParent(parent);

            return collidersRoot;
        }

        public static Collider CreateBorderCollider(string name, Transform parent, Vector3 size, Vector3 position, float yRotation)
        {
            var collider = new GameObject(name).AddComponent<BoxCollider>();
            collider.transform.SetParent(parent);

            collider.size = size;
            collider.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yRotation, 0));

            return collider;
        }

        public Terrain CreateTerrainChunk(TerrainData terrainData, Transform parent, int2 at, Material material, bool drawTreesAndFoliage)
        {
            Terrain terrain = Terrain.CreateTerrainGameObject(terrainData)
                                     .GetComponent<Terrain>();

            terrain.shadowCastingMode = ShadowCastingMode.Off;
            terrain.materialTemplate = material;
            terrain.detailObjectDistance = 200;
            terrain.enableHeightmapRayTracing = false;
            terrain.drawHeightmap = true; // forced to true for the color map renderer
            terrain.drawTreesAndFoliage = drawTreesAndFoliage;

            terrain.transform.position = new Vector3(at.x, -terrainGenData.minHeight, at.y);
            terrain.transform.SetParent(parent, false);

            return terrain;
        }

        public TreePrototype[] GetTreePrototypes()
        {
            if (treePrototypes != null)
                return treePrototypes;

            treePrototypes = terrainGenData.treeAssets.Select(t => new TreePrototype
                                            {
                                                prefab = t.asset,
                                            })
                                           .ToArray();

            return treePrototypes;
        }

        public DetailPrototype[] GetDetailPrototypes()
        {
            return terrainGenData.detailAssets.Select(a =>
                                  {
                                      var detailPrototype = new DetailPrototype
                                      {
                                          usePrototypeMesh = true,
                                          prototype = a.asset,
                                          useInstancing = true,
                                          renderMode = DetailRenderMode.VertexLit,
                                          density = a.TerrainDetailSettings.detailDensity,
                                          alignToGround = a.TerrainDetailSettings.alignToGround / 100f,
                                          holeEdgePadding = a.TerrainDetailSettings.holeEdgePadding / 100f,
                                          minWidth = a.TerrainDetailSettings.minWidth,
                                          maxWidth = a.TerrainDetailSettings.maxWidth,
                                          minHeight = a.TerrainDetailSettings.minHeight,
                                          maxHeight = a.TerrainDetailSettings.maxHeight,
                                          noiseSeed = a.TerrainDetailSettings.noiseSeed,
                                          noiseSpread = a.TerrainDetailSettings.noiseSpread,
                                          useDensityScaling = a.TerrainDetailSettings.affectedByGlobalDensityScale,
                                          positionJitter = a.TerrainDetailSettings.positionJitter / 100f,
                                      };

                                      return detailPrototype;
                                  })
                                 .ToArray();
        }
    }
}
