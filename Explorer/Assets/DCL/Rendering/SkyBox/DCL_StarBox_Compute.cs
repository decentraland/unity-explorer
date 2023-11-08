using UnityEngine;

public class DCL_StarBox_Compute
{
    private int nDimensions = 4096;
    private int nArraySize = 6;
    private ComputeShader StarsComputeShader;
    private RenderTexture CubemapTextureArray;

    // Use this for initialization
    public void Start()
    {
        // Texture2DArray tex2DArr = new Texture2DArray(nDimensions, nDimensions, nArraySize, TextureFormat.BC7, false, false);
        // tex2DArr.filterMode = FilterMode.Bilinear;
        // tex2DArr.wrapMode = TextureWrapMode.Repeat;

        // Create Render Texture
        CubemapTextureArray = new RenderTexture(nDimensions, nDimensions, nArraySize, RenderTextureFormat.ARGB32);
        {
            // CubemapTextureArray.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
            // CubemapTextureArray.volumeDepth = 6;
            CubemapTextureArray.wrapMode = TextureWrapMode.Clamp;
            CubemapTextureArray.filterMode = FilterMode.Trilinear;
            CubemapTextureArray.enableRandomWrite = true;
            CubemapTextureArray.isPowerOfTwo = true;
            CubemapTextureArray.Create();
        }
    }

    private int counter = 0;
    public void Update()
    {
        // Draw to Render Texture with Compute shader every few seconds
        // So feel free to recompile the compute shader while the editor is running
        if (counter == 0)
        {
            Instantiate(StarsComputeShader);
            string kernelName = "CSMain";
            int kernelIndex = StarsComputeShader.FindKernel(kernelName);

            StarsComputeShader.SetTexture(kernelIndex, "o_cubeMap", CubemapTextureArray);
            StarsComputeShader.SetInt("i_dimensions", nDimensions);
            StarsComputeShader.Dispatch(kernelIndex, nDimensions, nDimensions, 1);
        }
        counter = (counter + 1) % 300;
    }
}
