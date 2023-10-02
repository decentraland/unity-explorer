using DCL.AvatarRendering.AvatarShape.ComputeShader;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.Avatar
{
    public class TextureArrayContainer
    {
        private readonly TextureArray[] textureArrayTypes;
        private readonly int textureArraySize = 512;

        public TextureArrayContainer()
        {
            textureArrayTypes = new TextureArray[1];
            textureArrayTypes[(int)ComputeShaderConstants.TextureArrayType.ALBEDO] = new TextureArray(textureArraySize, ComputeShaderConstants._BaseMapArr_ShaderID, ComputeShaderConstants._BaseMapArrTex_ShaderID);
        }

        public UsedTextureArraySlot SetTexture(Material material, Texture2D texture, ComputeShaderConstants.TextureArrayType type) =>
            textureArrayTypes[(int)type].SetTexture(type, material, texture);

        public void FreeTexture(UsedTextureArraySlot usedTextureArraySlot)
        {
            textureArrayTypes[(int)usedTextureArraySlot.TextureArrayType].FreeTexture(usedTextureArraySlot);
        }
    }
}
