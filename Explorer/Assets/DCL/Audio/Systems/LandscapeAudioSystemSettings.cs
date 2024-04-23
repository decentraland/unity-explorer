using UnityEngine;

namespace DCL.Audio.System
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
        [SerializeField] private float mutingDistanceThreshold = 10f;
        [SerializeField] private int cellsPerChunk = 9;
        [SerializeField] private int systemUpdateFrequency = 10;
        [SerializeField] private int audioSourcePositioningRetryAttempts = 3;
        [SerializeField] private int oceanDistanceThreshold = 150;
        public float ListeningDistanceThreshold => mutingDistanceThreshold;
        public float MutingDistanceThreshold => distanceThreshold;
        public int RowsPerChunk => cellsPerChunk;
        public int SystemUpdateFrequency => systemUpdateFrequency;
        public int AudioSourcePositioningRetryAttempts => audioSourcePositioningRetryAttempts;
        public int OceanDistanceThreshold => oceanDistanceThreshold;
    }
}
