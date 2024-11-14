using UnityEngine;

namespace DCL.SDKComponents.MediaStream.Settings
{
    /// <summary>
    ///
    /// </summary>
    [CreateAssetMenu(fileName = "VideoPrioritizationSettings", menuName = "SO/VideoPrioritizationSettings", order = 0)]
    public class VideoPrioritizationSettings : ScriptableObject
    {
        [Tooltip("")]
        [Range(0.0f, 10.0f)]
        public float SizeInScreenWeight = 1.0f;

        [Tooltip("")]
        [Range(0.0f, 10.0f)]
        public float DistanceWeight = 1.0f;

        [Tooltip("")]
        [Range(0.0f, 10.0f)]
        public float AngleWeight = 1.0f;

        [Tooltip("")]
        [Range(0.0001f, 0.9999f)]
        public float MinimumSizeLimit = 0.1f;

        [Tooltip("")]
        public float MaximumDistanceLimit = 100.0f;
    }
}
