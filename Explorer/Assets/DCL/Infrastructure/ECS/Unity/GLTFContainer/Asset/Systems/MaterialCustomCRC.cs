using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.RenderSystem
{
    public class MaterialCRC
    {
        internal static readonly int SRC_BLEND_ID = Shader.PropertyToID("_SrcBlend");
        internal static readonly int DST_BLEND_ID = Shader.PropertyToID("_DstBlend");
        internal static readonly int ZWRITE_ID = Shader.PropertyToID("_ZWrite");
        internal static readonly int SRC_BLEND_ALPHA_ID = Shader.PropertyToID("_SrcBlendAlpha");
        internal static readonly int DST_BLEND_ALPHA_ID = Shader.PropertyToID("_DstBlendAlpha");
        internal static readonly int BLEND_OP_ID = Shader.PropertyToID("_BlendOp");
        internal static readonly int BLEND_OP_ALPHA_ID = Shader.PropertyToID("_BlendOpAlpha");
        internal static readonly int CULL_ID = Shader.PropertyToID("_Cull");
        internal static readonly int ALPHA_TO_MASK_ID = Shader.PropertyToID("_AlphaToMask");
        internal static readonly int SURFACE_ID = Shader.PropertyToID("_Surface");
        internal static readonly int PLANE_CLIPPING_ID = Shader.PropertyToID("_PlaneClipping");
        internal static readonly int VERTICAL_CLIPPING_ID = Shader.PropertyToID("_VerticalClipping");

        public static uint ComputeCustomMaterialCRC(Material mat)
        {
            uint crc = 0xFFFFFFFF; // CRCBegin()

            // Feed only the properties you care about
            crc = CustomCRC.CRCFeed(crc, mat.shader.GetInstanceID());
            crc = CustomCRC.CRCFeed(crc, mat.renderQueue);
            crc = CustomCRC.CRCFeed(crc, mat.enableInstancing);
            crc = CustomCRC.CRCFeed(crc, mat.doubleSidedGI);

            crc = CustomCRC.CRCFeed(crc, mat.GetFloat(SRC_BLEND_ID));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat(DST_BLEND_ID));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat(ZWRITE_ID));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat(SRC_BLEND_ALPHA_ID));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat(DST_BLEND_ALPHA_ID));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat(BLEND_OP_ID));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat(BLEND_OP_ALPHA_ID));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat(CULL_ID));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat(ALPHA_TO_MASK_ID));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat(SURFACE_ID));
            crc = CustomCRC.CRCFeed(crc, mat.GetVector(PLANE_CLIPPING_ID));
            crc = CustomCRC.CRCFeed(crc, mat.GetVector(VERTICAL_CLIPPING_ID));

            // Feed shader keywords (like Unity does with LocalKeywordState)
            var keywords = mat.shaderKeywords;
            System.Array.Sort(keywords); // Ensure consistent ordering

            foreach (var keyword in keywords)
            {
                byte[] keywordBytes = System.Text.Encoding.UTF8.GetBytes(keyword);
                crc = CustomCRC.CRCFeed(crc, keywordBytes);
            }

            int[] propIDArr = mat.GetTexturePropertyNameIDs();
            for (int i = 0; i < propIDArr.Length; ++i)
            {
                if (mat.GetTexture(propIDArr[i]) != null)
                {
                    crc = CustomCRC.CRCFeed(crc, mat.GetTexture(propIDArr[i]).GetInstanceID());
                }
            }

            return ~crc; // CRCDone()
        }
    }
}
