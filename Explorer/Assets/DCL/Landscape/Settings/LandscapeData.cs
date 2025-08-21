using DCL.Landscape.Utils;
using System;
using UnityEngine;

namespace DCL.Landscape.Settings
{
    public class LandscapeData : ScriptableObject
    {
        public Action<float> OnDetailDistanceChanged;

        public bool showSatelliteView;
        public bool drawTerrain;
        public bool drawTerrainDetails;
        public Transform mapChunk;
        public TerrainGenerationData terrainData;
        public TerrainGenerationData worldsTerrainData;

#if GPUI_PRO_PRESENT
        public GPUIAssets gpuiAssets;
        public const bool LOAD_TREES_FROM_STREAMINGASSETS = false;
#else
        public const bool LOAD_TREES_FROM_STREAMINGASSETS = false;
#endif

        [SerializeField] private float detailDistanceValue = 200;
        public float DetailDistance
        {
            get => detailDistanceValue;

            set
            {
                if (Mathf.Approximately(detailDistanceValue, value))
                    return;

                detailDistanceValue = value;
                OnDetailDistanceChanged?.Invoke(value);
            }
        }

        public bool RenderGround { get; set; }
        [field: SerializeField] public Material GroundMaterial { get; private set; } = null!;
        [field: SerializeField] public int GroundInstanceCapacity { get; set; }

        [field: SerializeField, EnumIndexedArray(typeof(GroundMeshPiece))]
        public Mesh[] GroundMeshes { get; private set; } = null!;

        private enum GroundMeshPiece
        {
            MIDDLE,
            EDGE,
            CORNER,
        }
    }
}
