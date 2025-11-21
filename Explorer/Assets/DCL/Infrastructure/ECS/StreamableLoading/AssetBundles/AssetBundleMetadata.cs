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

        // Note: The order of the outcomes is the same as the order in which they appear in the Emote DTO metadata
        public List<SocialEmoteOutcomeAnimationPose>? socialEmoteOutcomeAnimationStartPoses;

        public void Clear()
        {
            timestamp = -1;
            version = "1.0";
            dependencies.Clear();
            mainAsset = "";
            socialEmoteOutcomeAnimationStartPoses?.Clear();
        }
    }
}
