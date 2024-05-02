using UnityEngine;

namespace DCL.Audio
{
    public interface ILandscapeAudioSystemSettings
    {
        float ListeningDistanceThreshold { get; }
        float MutingDistanceThreshold { get; }
        int RowsPerChunk { get; }
        int SystemUpdateFrequency { get; }
        int AudioSourcePositioningRetryAttempts { get; }
        int OceanDistanceThreshold { get; }
    }

    [CreateAssetMenu(fileName = "LandscapeAudioSystemSettings", menuName = "SO/Audio/LandscapeAudioSystemSettings")]
    public class LandscapeAudioSystemSettings : ScriptableObject, ILandscapeAudioSystemSettings
    {
        [SerializeField] private float distanceThreshold = 10f;
        [SerializeField] private float mutingDistanceThreshold = 20f;
        [SerializeField] private int rowsPerChunk = 3;
        [SerializeField] private int systemUpdateFrequency = 10;
        [SerializeField] private int audioSourcePositioningRetryAttempts = 3;
        [SerializeField] private int oceanDistanceThreshold = 150;
        public float ListeningDistanceThreshold => distanceThreshold;
        public float MutingDistanceThreshold => mutingDistanceThreshold;
        public int RowsPerChunk => rowsPerChunk;
        public int SystemUpdateFrequency => systemUpdateFrequency;
        public int AudioSourcePositioningRetryAttempts => audioSourcePositioningRetryAttempts;
        public int OceanDistanceThreshold => oceanDistanceThreshold;
    }
}
