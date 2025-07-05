using System;
using UnityEngine;
using TerrainData = Decentraland.Terrain.TerrainData;

namespace DCL.Landscape.Settings
{
    public class LandscapeData : ScriptableObject
    {
        public Action<float> OnEnvironmentDistanceChanged;

        public bool showSatelliteView;
        public Transform mapChunk;
        public TerrainGenerationData genesisCityData;
        public TerrainGenerationData worldData;
        public TerrainData terrainData;

        // This is the source of truth for the "environment distance" slider in the graphics settings of
        // the game. This controls the distance up to which grass, streets, cliffs, and water are
        // rendered.
        [SerializeField] private float environmentDistance = 200;

        public float EnvironmentDistance
        {
            get => environmentDistance;

            set
            {
                if (Mathf.Approximately(environmentDistance, value))
                    return;

                environmentDistance = value;
                //terrainData.detailDistance = value;
                OnEnvironmentDistanceChanged?.Invoke(value);
            }
        }
    }
}
