using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureArrayContainer
{
    public Texture2DArray texture2DArray_BaseMap;
    public Texture2DArray texture2DArray_AlphaTexture;
    public Texture2DArray texture2DArray_MetallicGlossMap;
    public Texture2DArray texture2DArray_BumpMap;
    public Texture2DArray texture2DArray_EmissionMap;

    public TextureArrayContainer()
    {
        texture2DArray_BaseMap = new Texture2DArray(1024, 1024, 8, TextureFormat.DXT1, false, false);
        texture2DArray_BaseMap.filterMode = FilterMode.Bilinear;
        texture2DArray_BaseMap.wrapMode = TextureWrapMode.Repeat;

        texture2DArray_AlphaTexture = new Texture2DArray(1024, 1024, 8, TextureFormat.DXT1, false, false);
        texture2DArray_AlphaTexture.filterMode = FilterMode.Bilinear;
        texture2DArray_AlphaTexture.wrapMode = TextureWrapMode.Repeat;

        texture2DArray_MetallicGlossMap = new Texture2DArray(1024, 1024, 8, TextureFormat.DXT1, false, false);
        texture2DArray_MetallicGlossMap.filterMode = FilterMode.Bilinear;
        texture2DArray_MetallicGlossMap.wrapMode = TextureWrapMode.Repeat;

        texture2DArray_BumpMap = new Texture2DArray(1024, 1024, 8, TextureFormat.DXT1, false, false);
        texture2DArray_BumpMap.filterMode = FilterMode.Bilinear;
        texture2DArray_BumpMap.wrapMode = TextureWrapMode.Repeat;

        texture2DArray_EmissionMap = new Texture2DArray(1024, 1024, 8, TextureFormat.DXT1, false, false);
        texture2DArray_EmissionMap.filterMode = FilterMode.Bilinear;
        texture2DArray_EmissionMap.wrapMode = TextureWrapMode.Repeat;
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
