using System;
using UnityEngine;

public class TextureArrayCreator : MonoBehaviour
{

    [SerializeField]
    private Texture2D[] ordinaryTextures;
    private Texture2DArray texture2DArray;

    public Texture2DArray Texture2DArray => texture2DArray;
    public static TextureArrayCreator Instance;

    void Awake()
    {
        CreateTextureArray();
        Instance = this;
    }

    private void CreateTextureArray()
    {
        // Create Texture2DArray
        texture2DArray = new
            Texture2DArray(ordinaryTextures[0].width,
                ordinaryTextures[0].height, ordinaryTextures.Length,
                TextureFormat.RGBA32, true, false);

        // Apply settings
        texture2DArray.filterMode = FilterMode.Bilinear;
        texture2DArray.wrapMode = TextureWrapMode.Repeat;

        // Loop through ordinary textures and copy pixels to the
        // Texture2DArray
        for (var i = 0; i < ordinaryTextures.Length; i++)
        {
            texture2DArray.SetPixels(ordinaryTextures[i].GetPixels(0),
                i, 0);
        }

        // Apply our changes
        texture2DArray.Apply();
    }

}
