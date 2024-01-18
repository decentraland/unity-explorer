using DCL.Landscape.Config;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Landscape.Settings
{
    public class TerrainGenerationData : ScriptableObject
    {
        public TextAsset ownedParcels;

        [Header("General Settings")]
        public int terrainSize = 4800;
        public int chunkSize = 512;
        public int terrainScale = 15;
        public float heightScaleNerf = 1;
        public int seed;

        [Header("Textures")]
        public Material terrainMaterial;
        public TerrainLayer[] terrainLayers;
        public List<NoiseData> layerNoise;

        [Header("Trees")]
        public GameObject[] prefabs;
        public NoiseData treeNoise;
    }
}
