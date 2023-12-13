using UnityEngine;

namespace DCL.Landscape.Config
{
    [CreateAssetMenu(menuName = "Landscape/Landscape Asset", fileName = "LandscapeAsset", order = 0)]
    public class LandscapeAsset : ScriptableObject
    {
        public Transform asset;
        public ObjectRandomization randomization;
        public NoiseData noiseData;
        public int poolPreWarmCount;
    }
}
