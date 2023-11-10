using UnityEngine;
using UnityEngine.Pool;
using Utility;

public class DCL_StarBox_Compute
{

    //private IObjectPool<ComputeShader> computeShaderPool;

    public DCL_StarBox_Compute(ComputeShader _shader)
    {
        //StarsComputeShader = _shader;
    }

    // Use this for initialization
    public void Start()
    {
        // Texture2DArray tex2DArr = new Texture2DArray(nDimensions, nDimensions, nArraySize, TextureFormat.BC7, false, false);
        // tex2DArr.filterMode = FilterMode.Bilinear;
        // tex2DArr.wrapMode = TextureWrapMode.Repeat;

        // Create Render Texture

    }

    private int counter = 0;
    public void Update()
    {
        // Draw to Render Texture with Compute shader every few seconds
        // So feel free to recompile the compute shader while the editor is running
        if (counter == 0)
        {
            //ProvidedAsset<ComputeShader> providedComputeShader = await assetsProvisioner.ProvideMainAssetAsync(settings.computeShader, ct: ct);
            //computeShaderPool = new ObjectPool<ComputeShader>(() => Object.Instantiate(StarsComputeShader), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: 1);
            //Object.Instantiate(StarsComputeShader);
            //Instantiate(StarsComputeShader);
            //StarsComputeShader = computeShaderPool.Get();
            // string kernelName = "CSMain";
            // int kernelIndex = StarsComputeShader.FindKernel(kernelName);
            //
            // StarsComputeShader.SetTexture(kernelIndex, "o_cubeMap", CubemapTextureArray);
            // StarsComputeShader.SetInt("i_dimensions", nDimensions);
            // StarsComputeShader.Dispatch(kernelIndex, nDimensions, nDimensions, 1);
        }
        counter = (counter + 1) % 300;
    }
}
