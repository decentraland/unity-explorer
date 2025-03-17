using System;
using UnityEngine;

namespace DCL.Landscape.Config
{
    [Serializable]
    public class TerrainDetailSettings
    {
        [Range(0f, 100f)]
        public float alignToGround = 100f;

        [Range(0f, 100f)]
        public float positionJitter = 50f;

        public float minWidth = 1;
        public float maxWidth = 1;
        public float minHeight = 1;
        public float maxHeight = 1;
        public int noiseSeed = 1;
        public float noiseSpread = 100;

        [Range(0f, 100f)]
        public float holeEdgePadding = 75f;

        [Range(0f, 6f)]
        public float detailDensity = 1;

        public bool affectedByGlobalDensityScale = true;
    }
}
