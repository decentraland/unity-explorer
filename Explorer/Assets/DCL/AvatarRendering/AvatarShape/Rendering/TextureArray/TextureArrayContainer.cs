using DCL.AvatarRendering.AvatarShape.ComputeShader;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.Avatar
{
    public class TextureArrayContainer
    {
        internal readonly TextureArrayType[] textureArrayTypes;
        private readonly int textureArraySize = 500;

        public TextureArrayContainer()
        {
            textureArrayTypes = new TextureArrayType[1];
            textureArrayTypes[(int)ComputeShaderConstants.TextureArrayType.ALBEDO] = new TextureArrayType(textureArraySize, ComputeShaderConstants._BaseMapArr_ShaderID, ComputeShaderConstants._BaseMapArrTex_ShaderID);
        }

        public TextureArraySlot SetTexture(Material material, Texture2D texture, ComputeShaderConstants.TextureArrayType type) =>
            textureArrayTypes[(int)type].SetTexture(material, texture);

    }
}
