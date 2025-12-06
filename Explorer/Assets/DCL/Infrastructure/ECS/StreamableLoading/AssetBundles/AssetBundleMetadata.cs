using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    [Serializable]
    public class AssetBundleMetadata
    {
        [Serializable]
        public struct SocialEmoteOutcomeAnimationPose
        {
            public Vector3 Position;
            public Quaternion Rotation;

            public SocialEmoteOutcomeAnimationPose(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }
        }

        public long timestamp = -1;
        public string version = "1.0";
        public List<string> dependencies;
        public string mainAsset;

        public void Clear()
        {
            timestamp = -1;
            version = "1.0";
            dependencies.Clear();
            mainAsset = "";
        }
    }
}
