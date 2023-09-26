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
            textureArrayTypes[(int)ComputeShaderHelpers.TextureArrayType.ALBEDO] = new TextureArray(textureArraySize, ComputeShaderHelpers._BaseMapArr_ShaderID, ComputeShaderHelpers._BaseMapArrTex_ShaderID);
        }

        public UsedTextureArraySlot SetTexture(Material material, Texture2D texture, ComputeShaderHelpers.TextureArrayType type) =>
            textureArrayTypes[(int)type].SetTexture(type, material, texture);

        public void FreeTexture(UsedTextureArraySlot usedTextureArraySlot)
        {
            textureArrayTypes[(int)usedTextureArraySlot.TextureArrayType].FreeTexture(usedTextureArraySlot);
        }
    }
}
