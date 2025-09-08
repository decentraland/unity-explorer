using DCL.Landscape.Utils;
using Decentraland.Terrain;
using GPUInstancerPro;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Landscape.Settings
{
    public class LandscapeData : ScriptableObject
    {
        public Action<float>? OnDetailDistanceChanged;

        public bool showSatelliteView;
        public bool drawTerrain;
        public bool drawTerrainDetails;
        public Transform mapChunk;
        public TerrainGenerationData terrainData;
        public TerrainGenerationData worldsTerrainData;

        [field: SerializeField] public GPUIProfile TreesProfile { get; private set; } = null!;

        [Obsolete]
        public const bool LOAD_TREES_FROM_STREAMINGASSETS = true;

        [SerializeField] private float detailDistanceValue = 200;
        public float DetailDistance
        {
            get => detailDistanceValue;

            set
            {
                if (Mathf.Approximately(detailDistanceValue, value))
                    return;

                detailDistanceValue = value;
                ApplyDetailDistanceToTrees(value);
                OnDetailDistanceChanged?.Invoke(value);
            }
        }

        private void ApplyDetailDistanceToTrees(float distance)
        {
            TreesProfile.minMaxDistance = new Vector2(0, distance);
            TreesProfile.SetParameterBufferData();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                ApplyDetailDistanceToTrees(detailDistanceValue);
        }

        public bool RenderGround { get; set; } = true;
        [field: SerializeField] public Material? GroundMaterial { get; private set; }
        [field: SerializeField] public int GroundInstanceCapacity { get; set; }

        [Obsolete("Terrain Height is hardcoded nowadays")]
        [field: SerializeField] public int TerrainHeight { get; private set; }
        [field: SerializeField] public GrassIndirectRenderer? GrassIndirectRenderer { get; private set; }

        [field: SerializeField] [field: EnumIndexedArray(typeof(GroundMeshPiece))]
        public Mesh?[] GroundMeshes { get; private set; } = null!;

        private enum GroundMeshPiece
        {
            MIDDLE,
            EDGE,
            CORNER,
        }
    }

    [Serializable]
    public class LandscapeDataRef : AssetReferenceT<LandscapeData>
    {
        public LandscapeDataRef(string guid) : base(guid) { }
    }
}
