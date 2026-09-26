using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableTextureConstants
    {
        public static readonly int MAINTEX_ORIGINAL_TEXTURE = Shader.PropertyToID("_BaseMap");
        public static readonly int MASK_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_MaskTex");
    }
}
