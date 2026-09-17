using DCL.Landscape.Utils;
using Decentraland.Terrain;
using GPUInstancerPro;
using JetBrains.Annotations;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Landscape.Settings
{
    public class LandscapeData : ScriptableObject
    {
        // ReSharper disable InconsistentNaming
        public Transform mapChunk = null!;
        public TerrainGenerationData terrainData = null!;
        public TerrainGenerationData worldsTerrainData = null!;
        // ReSharper restore InconsistentNaming

        [SerializeField] private float detailDistanceValue = 200;

        [field: SerializeField] public Material? GroundMaterial { get; private set; }
        [field: SerializeField] public int GroundInstanceCapacity { get; set; }
        [field: SerializeField] public GrassIndirectRenderer? GrassIndirectRenderer { get; private set; }
        [field: SerializeField] [field: EnumIndexedArray(typeof(GroundMeshPiece))]
        public Mesh?[] GroundMeshes { get; private set; } = null!;

        [field: SerializeField] public GPUIProfile TreesProfile { get; private set; } = null!;

        public float DetailDistance
        {
            get => detailDistanceValue;

            set
            {
                if (Mathf.Approximately(detailDistanceValue, value))
                    return;

                detailDistanceValue = value;
                ApplyDetailDistanceToTrees(value);
            }
        }

        public bool RenderGround { get; set; } = true;
        public bool RenderTrees { get; set; } = true;
        public bool RenderGrass { get; set; } = true;
        public bool ShowSatelliteFloor { get; set; } = true;

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

        private enum GroundMeshPiece
        {
            [UsedImplicitly] Middle,
            [UsedImplicitly] Edge,
            [UsedImplicitly] Corner,
        }
    }

    [Serializable]
    public class LandscapeDataRef : AssetReferenceT<LandscapeData>
    {
        public LandscapeDataRef(string guid) : base(guid) { }
    }

    [Serializable]
    public class GpuiShaderBindingsRef : AssetReferenceT<GPUIShaderBindings>
    {
        public GpuiShaderBindingsRef(string guid) : base(guid) { }
    }
}
