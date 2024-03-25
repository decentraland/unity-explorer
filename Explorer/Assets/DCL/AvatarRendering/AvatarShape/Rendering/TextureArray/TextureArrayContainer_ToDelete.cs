using DCL.AvatarRendering.AvatarShape.ComputeShader;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.Avatar
{
    public class TextureArrayContainer_ToDelete
    {
        internal readonly TextureArrayType_ToDelete[] textureArrayTypes;
        private readonly int textureArraySize = 500;

        public TextureArrayContainer_ToDelete(TextureFormat textureFormat)
        {
            textureArrayTypes = new TextureArrayType_ToDelete[1];
            textureArrayTypes[(int)ComputeShaderConstants.TextureArrayType.ALBEDO] = new TextureArrayType_ToDelete(textureArraySize, ComputeShaderConstants._BaseMapArr_ShaderID, ComputeShaderConstants._BaseMapArrTex_ShaderID, textureFormat);
        }

        public TextureArraySlot_ToDelete SetTexture(Material material, Texture2D texture, ComputeShaderConstants.TextureArrayType type)
        {
            return textureArrayTypes[(int)type].SetTexture(material, texture);
        }
    }
}