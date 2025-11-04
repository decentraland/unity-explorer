using System.Collections.Generic;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    public struct InitialSceneStateMetadata
    {
        public List<string> assetHash;
        public List<Vector3> positions;
        public List<Quaternion> rotations;
        public List<Vector3> scales;
    }
}

