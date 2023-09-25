using UnityEngine;

public class TextureArrayContainer
{
    public Texture2DArray texture2DArray_512_BaseMap;
    public Texture2DArray texture2DArray_256_BaseMap;
    public Texture2DArray texture2DArray_AlphaTexture;
    public Texture2DArray texture2DArray_MetallicGlossMap;
    public Texture2DArray texture2DArray_BumpMap;
    public Texture2DArray texture2DArray_EmissionMap;

    public int textureArrayCount_512_BaseMap = 0;
    public int textureArrayCount_256_BaseMap = 0;

    public int textureArrayCount_AlphaTexture = 0;
    public int textureArrayCount_MetallicGlossMap = 0;
    public int textureArrayCount_BumpMap = 0;
    public int textureArrayCount_EmissionMap = 0;

    public int textureArraySize = 1024;

    public TextureArrayContainer()
    {
        texture2DArray_512_BaseMap = new Texture2DArray(512, 512, textureArraySize, TextureFormat.BC7, false, false);
        texture2DArray_512_BaseMap.filterMode = FilterMode.Bilinear;
        texture2DArray_512_BaseMap.wrapMode = TextureWrapMode.Repeat;

        texture2DArray_256_BaseMap = new Texture2DArray(256, 256, textureArraySize, TextureFormat.BC7, false, false);
        texture2DArray_256_BaseMap.filterMode = FilterMode.Bilinear;
        texture2DArray_256_BaseMap.wrapMode = TextureWrapMode.Repeat;

        texture2DArray_AlphaTexture = new Texture2DArray(1024, 1024, textureArraySize, TextureFormat.DXT1, false, false);
        texture2DArray_AlphaTexture.filterMode = FilterMode.Bilinear;
        texture2DArray_AlphaTexture.wrapMode = TextureWrapMode.Repeat;

        texture2DArray_MetallicGlossMap = new Texture2DArray(1024, 1024, textureArraySize, TextureFormat.DXT1, false, false);
        texture2DArray_MetallicGlossMap.filterMode = FilterMode.Bilinear;
        texture2DArray_MetallicGlossMap.wrapMode = TextureWrapMode.Repeat;

        texture2DArray_BumpMap = new Texture2DArray(1024, 1024, textureArraySize, TextureFormat.DXT1, false, false);
        texture2DArray_BumpMap.filterMode = FilterMode.Bilinear;
        texture2DArray_BumpMap.wrapMode = TextureWrapMode.Repeat;

        texture2DArray_EmissionMap = new Texture2DArray(1024, 1024, textureArraySize, TextureFormat.DXT1, false, false);
        texture2DArray_EmissionMap.filterMode = FilterMode.Bilinear;
        texture2DArray_EmissionMap.wrapMode = TextureWrapMode.Repeat;
    }
}
