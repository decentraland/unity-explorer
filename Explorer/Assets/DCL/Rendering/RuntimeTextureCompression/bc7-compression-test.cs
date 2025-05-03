using UnityEngine;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;

public class BC7CompressionTest : MonoBehaviour
{
    [SerializeField]
    private ComputeShader bc7ComputeShader;

    [SerializeField]
    private string inputPngPath = "Assets/TestImage.png";

    [SerializeField]
    private string outputPath = "Assets/CompressedImage.bc7";

    // Structure to match bc7_encoded_block in the compute shader
    struct BC7EncodedBlock
    {
        public uint4 m_bits;
    }

    void Start()
    {
        CompressImageToBC7();
    }

    void CompressImageToBC7()
    {
        // Load the PNG file
        byte[] pngData = File.ReadAllBytes(inputPngPath);

        // Create a temporary texture to load the PNG
        Texture2D sourceTexture = new Texture2D(2, 2);
        sourceTexture.LoadImage(pngData);

        // Ensure the texture dimensions are multiples of 4
        if (sourceTexture.width % 4 != 0 || sourceTexture.height % 4 != 0)
        {
            Debug.LogError("Texture dimensions must be multiples of 4 for BC7 compression!");
            return;
        }

        // Calculate blocks
        int widthInBlocks = sourceTexture.width / 4;
        int heightInBlocks = sourceTexture.height / 4;
        int totalBlocks = widthInBlocks * heightInBlocks;

        // Create a readwrite texture for the compute shader
        RenderTexture readableTexture = new RenderTexture(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32)
        {
            enableRandomWrite = true
        };
        readableTexture.Create();

        // Copy the texture data to the render texture
        Graphics.Blit(sourceTexture, readableTexture);

        // Set up the compute shader
        int kernelIndex = bc7ComputeShader.FindKernel("BC7Compress");

        // Create output buffer for compressed blocks
        ComputeBuffer outputBuffer = new ComputeBuffer(totalBlocks, sizeof(uint) * 4);

        // Set compute shader parameters
        bc7ComputeShader.SetTexture(kernelIndex, "SourceTexture", readableTexture);
        bc7ComputeShader.SetBuffer(kernelIndex, "EncodedBlocks", outputBuffer);
        bc7ComputeShader.SetInt("widthInBlocks", widthInBlocks);
        bc7ComputeShader.SetInt("heightInBlocks", heightInBlocks);

        // Calculate thread groups (8x8 threads per group)
        int threadGroupsX = Mathf.CeilToInt(widthInBlocks / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(heightInBlocks / 8.0f);

        // Dispatch the compute shader
        bc7ComputeShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);

        // Read back the compressed data
        BC7EncodedBlock[] compressedBlocks = new BC7EncodedBlock[totalBlocks];
        outputBuffer.GetData(compressedBlocks);

        // Save the compressed data to disk
        SaveBC7Data(compressedBlocks, widthInBlocks, heightInBlocks);

        // Cleanup
        outputBuffer.Release();
        readableTexture.Release();
        Destroy(sourceTexture);

        Debug.Log($"Successfully compressed image to BC7 format: {outputPath}");
        Debug.Log($"Original size: {sourceTexture.width}x{sourceTexture.height}");
        Debug.Log($"Blocks: {widthInBlocks}x{heightInBlocks} = {totalBlocks} total blocks");
    }

    void SaveBC7Data(BC7EncodedBlock[] blocks, int widthInBlocks, int heightInBlocks)
    {
        // Create a simple header for the compressed file
        using (BinaryWriter writer = new BinaryWriter(File.Open(outputPath, FileMode.Create)))
        {
            // Write header
            writer.Write((uint)0x37434237); // "BC7\x37" magic number
            writer.Write((uint)widthInBlocks);
            writer.Write((uint)heightInBlocks);

            // Write compressed blocks
            foreach (var block in blocks)
            {
                writer.Write(block.m_bits.x);
                writer.Write(block.m_bits.y);
                writer.Write(block.m_bits.z);
                writer.Write(block.m_bits.w);
            }
        }
    }

    // Optional: Method to visualize the compressed result
    public void DecompressAndDisplay()
    {
        // This would require implementing BC7 decompression
        // For now, you can use tools like DirectXTex to view the compressed result
        Debug.Log("BC7 decompression not implemented. Use external tools to view the result.");
    }
}

// Extension method to help with uint4 serialization
public static class Extensions
{
    public static void Write(this BinaryWriter writer, UnityEngine.Vector4 vec)
    {
        writer.Write(vec.x);
        writer.Write(vec.y);
        writer.Write(vec.z);
        writer.Write(vec.w);
    }
}
