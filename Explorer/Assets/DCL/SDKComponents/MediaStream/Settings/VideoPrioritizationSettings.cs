using UnityEngine;

namespace DCL.SDKComponents.MediaStream.Settings
{
    /// <summary>
    /// The parameters that define how the video playback prioritization mechanism behaves. They affect how priorities are calculated and which are paused.
    /// </summary>
    [CreateAssetMenu(fileName = "VideoPrioritizationSettings", menuName = "SO/VideoPrioritizationSettings", order = 0)]
    public class VideoPrioritizationSettings : ScriptableObject
    {
        [Tooltip("The amount of videos that will be playing at the same time, at most, on camera.")]
        [Range(1, 50.0f)]
        [SerializeField]
        private int maximumSimultaneousVideos;

        [Tooltip("The weight of the 'size in screen of the video renderers' factor in the calculation of video priorities.")]
        [Range(0.0f, 10.0f)]
        [SerializeField]
        private float sizeInScreenWeight = 1.0f;

        [Tooltip("The weight of the 'distance from the camera to the video renderers' factor in the calculation of video priorities.")]
        [Range(0.0f, 10.0f)]
        [SerializeField]
        private float distanceWeight = 1.0f;

        [Tooltip("The weight of the 'angle of the camera with respect the video renderers' factor in the calculation of video priorities.")]
        [Range(0.0f, 10.0f)]
        [SerializeField]
        private float angleWeight = 1.0f;

        [Tooltip("A normalized value that determines which videos will be paused, without being prioritized, when their size in screen is below it.")]
        [Range(0.0001f, 0.9999f)]
        [SerializeField]
        private float minimumSizeLimit = 0.1f;

        [Tooltip("A value that determines which videos will be paused, without being prioritized, when their distance to the camera is above it.")]
        [SerializeField]
        private float maximumDistanceLimit = 100.0f;

        public delegate void MaximumSimultaneousVideosChangedDelegate(int newValue);

        /// <summary>
        /// Raised when the maximum simultaneous videos property changes.
        /// </summary>
        public event MaximumSimultaneousVideosChangedDelegate MaximumSimultaneousVideosChanged;

        /// <summary>
        /// Gets or sets the amount of videos that will be playing at the same time, at most, on camera.
        /// </summary>
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

        /// <summary>
        /// Gets the weight of the 'size in screen of the video renderers' factor in the calculation of video priorities.
        /// </summary>
        public float SizeInScreenWeight => sizeInScreenWeight;

        /// <summary>
        /// Gets the weight of the 'distance from the camera to the video renderers' factor in the calculation of video priorities.
        /// </summary>
        public float DistanceWeight => distanceWeight;

        /// <summary>
        /// Gets the weight of the 'angle of the camera with respect the video renderers' factor in the calculation of video priorities.
        /// </summary>
        public float AngleWeight => angleWeight;

        /// <summary>
        /// Gets a normalized value that determines which videos will be paused, without being prioritized, when their size in screen is below it.
        /// </summary>
        public float MinimumSizeLimit => minimumSizeLimit;

        /// <summary>
        /// Gets a value that determines which videos will be paused, without being prioritized, when their distance to the camera is above it.
        /// </summary>
        public float MaximumDistanceLimit => maximumDistanceLimit;
    }
}
