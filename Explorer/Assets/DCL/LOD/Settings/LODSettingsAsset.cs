using UnityEngine;

namespace DCL.LOD
{
    [CreateAssetMenu(menuName = "Create LOD Settings", fileName = "LODSettings", order = 0)]
    public class LODSettingsAsset : ScriptableObject, ILODSettingsAsset
    {
        //Threshold for bucket partition (inclusive) 
        [field: SerializeField] public int[] LodPartitionBucketThresholds { get; set; } = { 1, 2, 5 };
        [field: SerializeField] public bool IsColorDebuging { get; set; }
        [field: SerializeField] public Color[] LODDebugColors { get; set; } = { Color.green, Color.yellow, Color.red };
        [field: SerializeField] public GameObject FaillingCube { get; set; }
    }
}
