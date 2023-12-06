using UnityEngine;

namespace DCL.Landscape.Config
{
    [CreateAssetMenu(menuName = "Landscape/Noise Data", fileName = "NoiseData", order = 0)]
    public class NoiseData : ScriptableObject
    {
        public NoiseSettings settings;
    }
}
