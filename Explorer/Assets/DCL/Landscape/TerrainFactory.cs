using DCL.Landscape.Settings;
using UnityEngine;
using Utility;

namespace DCL.Landscape
{
    public class TerrainFactory
    {
        private const string BORDERS_LAYER = "InvisibleColliders";

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
            side.name = "Cliff side " + at;
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
            collider.gameObject.layer = LayerMask.NameToLayer(BORDERS_LAYER);

            collider.size = size;
            collider.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yRotation, 0));

            return collider;
        }
    }
}
