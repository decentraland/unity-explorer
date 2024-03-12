using UnityEngine;

namespace DCL.Landscape.Config
{
    [CreateAssetMenu(menuName = "Landscape/Landscape Asset", fileName = "LandscapeAsset", order = 0)]
    public class LandscapeAsset : ScriptableObject
    {
        public GameObject asset;

        /// <summary>
        ///     This radius is used by the placement system, to avoid overlapping with nearby owned scenes
        /// </summary>
        public NoiseDataBase noiseData;

        /// <summary>
        ///     The more density, the more assets inside a chunk
        /// </summary>
        [Header("Settings when used as tree")]
        public ObjectRandomization randomization;

        [Tooltip("This radius is being used for this asset to not overlap with the same asset type")]
        public float radius;

        [Tooltip("This radius is being used for this asset to not overlap with other asset types, you might want to set this value a bit lower than radius for nicer results, since your trees might get no stones below it")]
        public float secondaryRadius;

        [Header("Settings when used as detail")]
        public TerrainDetailSettings TerrainDetailSettings;
    }
}
