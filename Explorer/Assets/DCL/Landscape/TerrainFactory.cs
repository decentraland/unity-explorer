using DCL.Landscape.Settings;
using StylizedGrass;
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

        public Transform Root { get; private set; }

        public TerrainFactory(TerrainGenerationData terrainGenData)
        {
            this.terrainGenData = terrainGenData;
        }

        public Transform InstantiateSingletonTerrainRoot(string terrainObjectName)
        {
            if (Root != null)
                UnityObjectUtils.SafeDestroy(Root.gameObject);

            Root = new GameObject(terrainObjectName).transform;
            return Root;
        }

        public Transform CreateOcean(Transform parent, bool worldPositionStays = true) =>
            Object.Instantiate(terrainGenData.ocean, parent, worldPositionStays).transform;

        public Transform CreateWind() =>
            Object.Instantiate(terrainGenData.wind).transform;

        public Transform CreateCliffsRoot(Transform parent)
        {
            Transform cliffsRoot = new GameObject("Cliffs").transform;
            cliffsRoot.SetParent(parent);

            return cliffsRoot;
        }

        public Transform CreateCliffCorner(Transform parent, Vector3 at, Quaternion rotation)
        {
            Transform neCorner = Object.Instantiate(terrainGenData.cliffCorner, at, rotation).transform;
            neCorner.SetParent(parent, true);

            return neCorner;
        }

        public Transform CreateCliffSide(Transform parent, Vector3 at, Quaternion rotation)
        {
            Transform side = Object.Instantiate(terrainGenData.cliffSide, at, rotation).transform;
            side.SetParent(parent, true);

            return side;
        }

        public Transform CreateCollidersRoot(Transform parent)
        {
            Transform collidersRoot = new GameObject("BorderColliders").transform;
            collidersRoot.SetParent(parent);

            return collidersRoot;
        }

        public Collider CreateBorderCollider(string name, Transform parent, Vector3 size, Vector3 position, float yRotation)
        {
            BoxCollider collider = new GameObject(name).AddComponent<BoxCollider>();
            collider.transform.SetParent(parent);
            collider.gameObject.layer = LayerMask.NameToLayer("InvisibleColliders");

            collider.size = size;
            collider.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yRotation, 0));

            return collider;
        }

        public Terrain CreateTerrainObject(TerrainData terrainData, Transform parent, int2 at, Material material)
        {
            Terrain terrain = Terrain.CreateTerrainGameObject(terrainData)
                                     .GetComponent<Terrain>();

            terrain.shadowCastingMode = ShadowCastingMode.Off;
            terrain.materialTemplate = material;
            terrain.detailObjectDistance = 200;
            terrain.enableHeightmapRayTracing = false;
            terrain.drawHeightmap = true; // forced to true for the color map renderer
            terrain.drawTreesAndFoliage = true;

            terrain.transform.position = new Vector3(at.x, -terrainGenData.minHeight, at.y);
            terrain.transform.SetParent(parent, false);

            return terrain;
        }

        public TerrainData CreateTerrainData(int terrainChunkSize, float maxHeight) =>
            CreateTerrainData(terrainChunkSize, terrainChunkSize, terrainChunkSize, maxHeight);

        private TerrainData CreateTerrainData(int heightmapResolution, int alphamapResolution, int terrainChunkSize, float maxHeight)
        {
            var terrainData = new TerrainData
            {
                heightmapResolution = heightmapResolution + 1,
                alphamapResolution = alphamapResolution,
                size = new Vector3(terrainChunkSize, Mathf.Max(maxHeight, 0.1f), terrainChunkSize),
                terrainLayers = terrainGenData.terrainLayers,
                treePrototypes = GetTreePrototypes(),
                detailPrototypes = GetDetailPrototypes(),
            };

            terrainData.SetDetailResolution(terrainChunkSize, 32);

            return terrainData;
        }

        public (GrassColorMapRenderer colorMapRenderer, GrassColorMap grassColorMap) CreateColorMapRenderer(Transform parent)
        {
            GrassColorMapRenderer colorMapRenderer = Object.Instantiate(terrainGenData.grassRenderer, parent)
                                                           .GetComponent<GrassColorMapRenderer>();

            GrassColorMap grassColorMap = ScriptableObject.CreateInstance<GrassColorMap>();

            colorMapRenderer.colorMap = grassColorMap;
            colorMapRenderer.resolution = 2048;

            return (colorMapRenderer, grassColorMap);
        }

        private TreePrototype[] GetTreePrototypes()
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

        private DetailPrototype[] GetDetailPrototypes()
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
