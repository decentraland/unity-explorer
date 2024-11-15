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
        [Range(1, 50.0f)]
        [SerializeField]
        private int maximumSimultaneousVideos;

        [Tooltip("")]
        [Range(0.0f, 10.0f)]
        [SerializeField]
        private float sizeInScreenWeight = 1.0f;

        [Tooltip("")]
        [Range(0.0f, 10.0f)]
        [SerializeField]
        private float distanceWeight = 1.0f;

        [Tooltip("")]
        [Range(0.0f, 10.0f)]
        [SerializeField]
        private float angleWeight = 1.0f;

        [Tooltip("")]
        [Range(0.0001f, 0.9999f)]
        [SerializeField]
        private float minimumSizeLimit = 0.1f;

        [Tooltip("")]
        [SerializeField]
        private float maximumDistanceLimit = 100.0f;

        public delegate void MaximumSimultaneousVideosChangedDelegate(int newValue);
        public event MaximumSimultaneousVideosChangedDelegate MaximumSimultaneousVideosChanged;

        public int MaximumSimultaneousVideos
        {
            get => maximumSimultaneousVideos;

            set
            {
                if (value != maximumSimultaneousVideos)
                {
                    maximumSimultaneousVideos = Mathf.Clamp(value, 1, 50);
                    MaximumSimultaneousVideosChanged?.Invoke(value);
                }
            }
        }

        public float SizeInScreenWeight { get => sizeInScreenWeight; }

        public float DistanceWeight { get => distanceWeight; }

        public float AngleWeight { get => angleWeight; }

        public float MinimumSizeLimit { get => minimumSizeLimit; }

        public float MaximumDistanceLimit { get => maximumDistanceLimit; }
    }
}
