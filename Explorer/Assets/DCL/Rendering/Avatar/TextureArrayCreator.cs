using System;
using UnityEngine;

public class TextureArrayCreator : MonoBehaviour
{
    public Texture2D[] tex2D_BaseMap;
    public Texture2D[] tex2D_AlphaTexture;
    public Texture2D[] tex2D_MetallicGlossMap;
    public Texture2D[] tex2D_BumpMap;
    public Texture2D[] tex2D_EmissionMap;

    public TextureArrayContainer m_TextureArrays;
    public static TextureArrayCreator Instance;

    void Awake()
    {
        CreateTextureArray();
        Instance = this;
    }

    private void CreateTextureArray()
    {
        m_TextureArrays = new TextureArrayContainer();
        for (var i = 0; i < tex2D_BaseMap.Length; i++)
        {
            Graphics.CopyTexture(tex2D_BaseMap[i], srcElement: 0, srcMip: 0, m_TextureArrays.texture2DArray_BaseMap, dstElement: i, dstMip: 0);
        }

        for (var i = 0; i < tex2D_AlphaTexture.Length; i++)
        {
            Graphics.CopyTexture(tex2D_AlphaTexture[i], srcElement: 0, srcMip: 0, m_TextureArrays.texture2DArray_AlphaTexture, dstElement: i, dstMip: 0);
        }

        for (var i = 0; i < tex2D_MetallicGlossMap.Length; i++)
        {
            Graphics.CopyTexture(tex2D_MetallicGlossMap[i], srcElement: 0, srcMip: 0, m_TextureArrays.texture2DArray_MetallicGlossMap, dstElement: i, dstMip: 0);
        }

        for (var i = 0; i < tex2D_BumpMap.Length; i++)
        {
            Graphics.CopyTexture(tex2D_BumpMap[i], srcElement: 0, srcMip: 0, m_TextureArrays.texture2DArray_BumpMap, dstElement: i, dstMip: 0);
        }

        for (var i = 0; i < tex2D_EmissionMap.Length; i++)
        {
            Graphics.CopyTexture(tex2D_EmissionMap[i], srcElement: 0, srcMip: 0, m_TextureArrays.texture2DArray_EmissionMap, dstElement: i, dstMip: 0);
        }
    }

}
