using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.AvatarShape.GPUSkinning
{
    public class GPUSkinningComponent
    {
        public static List<SimpleGPUSkinning> DoSkinning(GameObject gameObject)
        {
            var gpuSkinnedRenderers = new List<SimpleGPUSkinning>();
            Transform rootTransform = gameObject.transform;
            SkinnedMeshRenderer[] renderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer r in renderers)
            {
                // Make sure that Transform is uniform with the root
                // Non-uniform does not make sense as skin relatively to the base avatar
                // so we just waste calculations on transformation matrices

                Transform currentTransform = r.transform;

                while (currentTransform != rootTransform)
                {
                    currentTransform.ResetLocalTRS();
                    currentTransform = currentTransform.parent;
                }

                var newSkinning = new SimpleGPUSkinning(r);
                gpuSkinnedRenderers.Add(newSkinning);
            }

            return gpuSkinnedRenderers;
        }

        public static List<SimpleComputeShaderSkinning> DoSkinningCompute(GameObject gameObject, Transform[] baseAvatarBones, Transform avatarBaseTransform) =>
            null;
    }
}
