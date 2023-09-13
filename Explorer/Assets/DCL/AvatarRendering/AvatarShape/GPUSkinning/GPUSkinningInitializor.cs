using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.GPUSkinning
{
    public class GPUSkinningComponent
    {
        public static List<SimpleGPUSkinning> DoSkinning(GameObject gameObject)
        {
            var gpuSkinnedRenderers = new List<SimpleGPUSkinning>();
            SkinnedMeshRenderer[] renderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer r in renderers)
            {
                var newSkinning = new SimpleGPUSkinning(r);
                gpuSkinnedRenderers.Add(newSkinning);
            }

            return gpuSkinnedRenderers;
        }
    }
}
