using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.RenderSystem
{
    public class MaterialCRC
    {
        public static uint ComputeCustomMaterialCRC(Material mat)
        {
            uint crc = 0xFFFFFFFF; // CRCBegin()

            // Feed only the properties you care about
            crc = CustomCRC.CRCFeed(crc, mat.shader.GetInstanceID());
            crc = CustomCRC.CRCFeed(crc, mat.renderQueue);
            crc = CustomCRC.CRCFeed(crc, mat.enableInstancing);
            crc = CustomCRC.CRCFeed(crc, mat.doubleSidedGI);

            crc = CustomCRC.CRCFeed(crc, mat.GetFloat("_SrcBlend"));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat("_DstBlend"));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat("_ZWrite"));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat("_SrcBlendAlpha"));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat("_DstBlendAlpha"));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat("_BlendOp"));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat("_BlendOpAlpha"));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat("_Cull"));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat("_AlphaToMask"));
            crc = CustomCRC.CRCFeed(crc, mat.GetFloat("_Surface"));

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
