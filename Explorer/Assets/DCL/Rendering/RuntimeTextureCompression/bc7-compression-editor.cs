using UnityEngine;
using UnityEditor;
using System.IO;

public class BC7CompressionEditor : EditorWindow
{
    private ComputeShader bc7ComputeShader;
    private Texture2D sourceTexture;
    private string outputPath = "Assets/CompressedImage.bc7";
    
    [MenuItem("Tools/BC7 Compression Tool")]
    public static void ShowWindow()
    {
        GetWindow<BC7CompressionEditor>("BC7 Compression");
    }
    
    void OnGUI()
    {
        GUILayout.Label("BC7 Texture Compression", EditorStyles.boldLabel);
        
        bc7ComputeShader = (ComputeShader)EditorGUILayout.ObjectField("BC7 Compute Shader", bc7ComputeShader, typeof(ComputeShader), false);
        sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source Texture", sourceTexture, typeof(Texture2D), false);
        
        EditorGUILayout.Space();
        
        outputPath = EditorGUILayout.TextField("Output Path", outputPath);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Compress to BC7"))
        {
            if (bc7ComputeShader == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign the BC7 compute shader", "OK");
                return;
            }
            
            if (sourceTexture == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a source texture", "OK");
                return;
            }
            
            CompressTextureToBC7();
        }
    }
    
    void CompressTextureToBC7()
    {
        // Ensure the texture is readable
        string assetPath = AssetDatabase.GetAssetPath(sourceTexture);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        
        // Ensure the texture dimensions are multiples of 4
        if (sourceTexture.width % 4 != 0 || sourceTexture.height % 4 != 0)
        {
            EditorUtility.DisplayDialog("Error", "Texture dimensions must be multiples of 4 for BC7 compression!", "OK");
            return;
        }
        
        // Calculate blocks
        int widthInBlocks = sourceTexture.width / 4;
        int heightInBlocks = sourceTexture.height / 4;
        int totalBlocks = widthInBlocks * heightInBlocks;
        
        // Create a temporary render texture from the source texture
        RenderTexture tempRT = new RenderTexture(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
        tempRT.enableRandomWrite = true;
        tempRT.Create();
        
        // Copy the texture to the render texture
        Graphics.Blit(sourceTexture, tempRT);
        
        // Set up the compute shader
        int kernelIndex = bc7ComputeShader.FindKernel("BC7Compress");
        
        // Create output buffer for compressed blocks
        ComputeBuffer outputBuffer = new ComputeBuffer(totalBlocks, sizeof(uint) * 4);
        
        // Set compute shader parameters
        bc7ComputeShader.SetTexture(kernelIndex, "SourceTexture", tempRT);
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
        tempRT.Release();
        
        EditorUtility.DisplayDialog("Success", $"Successfully compressed image to BC7 format: {outputPath}", "OK");
        Debug.Log($"Original size: {sourceTexture.width}x{sourceTexture.height}");
        Debug.Log($"Blocks: {widthInBlocks}x{heightInBlocks} = {totalBlocks} total blocks");
        
        AssetDatabase.Refresh();
    }
    
    void SaveBC7Data(BC7EncodedBlock[] blocks, int widthInBlocks, int heightInBlocks)
    {
        // Create a simple header for the compressed file
        string fullPath = Path.Combine(Application.dataPath, outputPath.Replace("Assets/", ""));
        string directory = Path.GetDirectoryName(fullPath);
        
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        
        using (BinaryWriter writer = new BinaryWriter(File.Open(fullPath, FileMode.Create)))
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
    
    // Structure to match bc7_encoded_block in the compute shader
    struct BC7EncodedBlock
    {
        public uint x;
        public uint y;
        public uint z;
        public uint w;
        
        public Vector4 m_bits => new Vector4(x, y, z, w);
    }
}
