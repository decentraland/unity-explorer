using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ECS.Prioritization
{
    [CreateAssetMenu(fileName = "PartitionSettings", menuName = "DCL/Prioritization/Partition Settings")]
    public class PartitionSettingsAsset : ScriptableObject, IPartitionSettings
    {
        [SerializeField] private float positionTolerance = 0.1f;
        [SerializeField] private int[] distanceBuckets = { 16, 32, 48, 96, 256, 512, 2048 };
        [SerializeField] private int fastPathDistance = 256;

        private void OnEnable()
        {
            CacheValues();
        }

        private void OnValidate()
        {
            CacheValues();
        }

        [field: SerializeField]
        public float AngleTolerance { get; private set; } = 1f;

        public float PositionSqrTolerance { get; private set; }
        public IReadOnlyList<int> SqrDistanceBuckets { get; private set; }
        public int FastPathSqrDistance { get; private set; }

        private void CacheValues()
        {
            PositionSqrTolerance = positionTolerance * positionTolerance;
            FastPathSqrDistance = fastPathDistance * fastPathDistance;
            SqrDistanceBuckets = new List<int>(distanceBuckets.Select(x => x * x));
        }
    }
}
