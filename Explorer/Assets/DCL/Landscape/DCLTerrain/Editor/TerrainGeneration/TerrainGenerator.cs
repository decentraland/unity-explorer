using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using Debug = UnityEngine.Debug;

public class TerrainGeneratorWithAnalysis : ScriptableWizard
{
    [Header("Compute Shaders")]
    public ComputeShader terrainHeightOnlyShader; // Height only for BC4
    public ComputeShader terrainAnalysisShader; // Analysis shader (AnalyzeTerrain kernel)
    public ComputeShader terrainReductionShader; // Reduction shader (ReduceResults kernel)
    public ComputeShader terrainOptimizationShader; // Optimization shader (OptimizeForBC4 kernel)
    public ComputeShader bc4CompressionShader; // BC4 compression

    [Header("Terrain Parameters")]
    [Range(0.001f, 1.0f)]
    public float frequency = 0.01f;

    [Range(1, 8)]
    public int octaves = 4;

    [Header("Texture Settings")]
    [Range(512, 8192)]
    public int textureSize = 8192;

    [Header("Analysis Settings")]
    [Range(0f, 1f)]
    public float optimizationStrength = 0.5f;
    public bool enableAnalysis = true;

    [Header("Output Settings")]
    public bool saveCompressedData = true;
    public bool saveMetadata = true;
    public string saveFileName = "TerrainHeightmap";

    [Header("Output Textures")]
    public RenderTexture rawTexture; // Raw terrain output
    public RenderTexture optimizedTexture; // Analyzed and optimized texture
    public Material previewMaterial;

    [Header("Debug Info")]
    [SerializeField] public float _lastMinHeight;
    [SerializeField] public float _lastMaxHeight;
    [SerializeField] public float _lastRangeWidth;
    [SerializeField] public float _lastPrecisionUsed;

    // Runtime metadata
    private Dictionary<string, object> _terrainMetadata = new ();

    // Compute buffers
    private ComputeBuffer analysisBuffer;
    private ComputeBuffer globalStatsBuffer;
    private ComputeBuffer compressedDataBuffer;

    // Kernel handles
    private int terrainKernelHandle;
    private int analysisKernelHandle;
    private int reductionKernelHandle;
    private int optimizationKernelHandle;
    private int bc4KernelHandle;

    private bool isInitialized;

    [MenuItem("Tools/TerrainGenerator")]
    private static void MenuItem()
    {
        DisplayWizard<TerrainGeneratorWithAnalysis>("Terrain Generator");
    }

    private void OnWizardCreate()
    {
        InitializeShaders();
        GenerateOptimizedTerrain();
    }

    private void InitializeShaders()
    {
        if (terrainHeightOnlyShader != null)
            terrainKernelHandle = terrainHeightOnlyShader.FindKernel("TerrainHeightOnly");

        if (terrainAnalysisShader != null)
            analysisKernelHandle = terrainAnalysisShader.FindKernel("AnalyzeTerrain");

        if (terrainReductionShader != null)
            reductionKernelHandle = terrainReductionShader.FindKernel("ReduceResults");

        if (terrainOptimizationShader != null)
            optimizationKernelHandle = terrainOptimizationShader.FindKernel("OptimizeForBC4");

        if (bc4CompressionShader != null)
            bc4KernelHandle = bc4CompressionShader.FindKernel("CompressToBC4");

        isInitialized = true;
    }

    [ContextMenu("Generate Optimized Terrain")]
    public void GenerateOptimizedTerrain()
    {
        if (!isInitialized)
            InitializeShaders();

        Debug.Log("Starting optimized terrain generation...");
        var stopwatch = Stopwatch.StartNew();

        // Step 1: Generate raw terrain
        GenerateRawTerrain();

        // Step 2: Analyze terrain (if enabled)
        if (enableAnalysis)
        {
            AnalyzeTerrain();
            OptimizeTerrain();
        }

        // Step 3: Compress to BC4
        if (saveCompressedData) { CompressToBC4(); }

        // Step 4: Save outputs
        SaveOutputs();

        stopwatch.Stop();
        Debug.Log($"Optimized terrain generation completed in {stopwatch.ElapsedMilliseconds}ms");

        // Update preview
        if (previewMaterial != null) { previewMaterial.mainTexture = optimizedTexture != null ? optimizedTexture : rawTexture; }
    }

    private void GenerateRawTerrain()
    {
        Debug.Log("Generating raw terrain...");

        // Create raw texture
        CreateTexture(ref rawTexture, "Raw");

        // Set parameters
        terrainHeightOnlyShader.SetTexture(terrainKernelHandle, "Result", rawTexture);
        terrainHeightOnlyShader.SetVector("_TerrainParams", new Vector4(frequency, 0, octaves, 0));
        terrainHeightOnlyShader.SetInts("_TextureSize", textureSize, textureSize);

        // Dispatch
        int threadGroups = Mathf.CeilToInt(textureSize / 8.0f);
        terrainHeightOnlyShader.Dispatch(terrainKernelHandle, threadGroups, threadGroups, 1);

        Debug.Log("Raw terrain generation complete");
    }

    private void AnalyzeTerrain()
    {
        Debug.Log("Analyzing terrain...");

        // Create analysis buffers
        CreateAnalysisBuffers();

        // Step 1: Analyze
        terrainAnalysisShader.SetTexture(analysisKernelHandle, "SourceTerrain", rawTexture);
        terrainAnalysisShader.SetBuffer(analysisKernelHandle, "AnalysisBuffer", analysisBuffer);
        terrainAnalysisShader.SetVector("TextureSize", new Vector2(textureSize, textureSize));

        int threadGroups = Mathf.CeilToInt(textureSize / 8.0f);
        terrainAnalysisShader.Dispatch(analysisKernelHandle, threadGroups, threadGroups, 1);

        // Step 2: GPU Reduction
        terrainReductionShader.SetBuffer(reductionKernelHandle, "AnalysisBuffer", analysisBuffer);
        terrainReductionShader.SetBuffer(reductionKernelHandle, "GlobalStats", globalStatsBuffer);
        terrainReductionShader.SetVector("TextureSize", new Vector2(textureSize, textureSize));

        int numWorkgroups = threadGroups * threadGroups;
        terrainReductionShader.SetInt("NumWorkgroups", numWorkgroups);

        // Single workgroup of 64 threads can handle all the reduction
        terrainReductionShader.Dispatch(reductionKernelHandle, 1, 1, 1);

        // Step 3: Read results
        var statsArray = new Vector4[1];
        globalStatsBuffer.GetData(statsArray);
        Vector4 stats = statsArray[0];

        // Store metadata
        _terrainMetadata = TerrainMetadataHelper.CreateMetadata(
            stats.x, stats.y, stats.z, stats.w,
            stats.y - stats.x, optimizationStrength, 1.0f,
            frequency, octaves, textureSize, textureSize
        );

        // Update debug display
        _lastMinHeight = stats.x;
        _lastMaxHeight = stats.y;
        _lastRangeWidth = stats.y - stats.x;
        _lastPrecisionUsed = (stats.y - stats.x) * 100f;

        Debug.Log($"Analysis complete - Range: [{stats.x:F6}, {stats.y:F6}], Width: {stats.y - stats.x:F6}");
    }

    private void OptimizeTerrain()
    {
        Debug.Log("Optimizing terrain for BC4...");

        // Create optimized texture
        CreateTexture(ref optimizedTexture, "Optimized");

        // Set parameters
        terrainOptimizationShader.SetTexture(optimizationKernelHandle, "SourceTerrain", rawTexture);
        terrainOptimizationShader.SetTexture(optimizationKernelHandle, "OptimizedTerrain", optimizedTexture);
        terrainOptimizationShader.SetBuffer(optimizationKernelHandle, "GlobalStats", globalStatsBuffer);
        terrainOptimizationShader.SetVector("TextureSize", new Vector2(textureSize, textureSize));
        terrainOptimizationShader.SetFloat("OptimizationStrength", optimizationStrength);

        // Dispatch
        int threadGroups = Mathf.CeilToInt(textureSize / 8.0f);
        terrainOptimizationShader.Dispatch(optimizationKernelHandle, threadGroups, threadGroups, 1);

        Debug.Log($"Optimization complete with strength {optimizationStrength:F2}");
    }

    private void CompressToBC4()
    {
        Debug.Log("Compressing to BC4...");

        RenderTexture sourceTexture = optimizedTexture != null ? optimizedTexture : rawTexture;

        // Calculate blocks
        int blocksX = Mathf.CeilToInt(textureSize / 4.0f);
        int blocksY = Mathf.CeilToInt(textureSize / 4.0f);
        int totalBlocks = blocksX * blocksY;

        // Create compression buffer
        if (compressedDataBuffer != null)
            compressedDataBuffer.Release();

        compressedDataBuffer = new ComputeBuffer(totalBlocks, sizeof(uint) * 2);

        // Set parameters
        bc4CompressionShader.SetInt("g_srcWidth", textureSize);
        bc4CompressionShader.SetInt("g_srcHeight", textureSize);
        bc4CompressionShader.SetInt("g_blocksX", blocksX);
        bc4CompressionShader.SetInt("g_blocksY", blocksY);
        bc4CompressionShader.SetInt("g_blockOffset", 0);
        bc4CompressionShader.SetTexture(bc4KernelHandle, "g_SourceTexture", sourceTexture);
        bc4CompressionShader.SetBuffer(bc4KernelHandle, "g_CompressedData", compressedDataBuffer);

        // Dispatch
        bc4CompressionShader.Dispatch(bc4KernelHandle, blocksX, blocksY, 1);

        Debug.Log($"BC4 compression complete - {totalBlocks * 8} bytes");
    }

    private void SaveOutputs()
    {
        Debug.Log("Saving outputs...");

        if (saveMetadata && _terrainMetadata.Count > 0)
        {
            string metadataJson = TerrainMetadataHelper.MetadataToJson(_terrainMetadata);
            string metadataPath = Path.Combine(Application.dataPath, $"{saveFileName}_metadata.json");
            File.WriteAllText(metadataPath, metadataJson);
            Debug.Log($"Metadata saved: {metadataPath}");
        }

        if (saveCompressedData && compressedDataBuffer != null) { SaveBC4CompressedData(); }

        SaveOptimizedTextureAsPNG();

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    private void CreateTexture(ref RenderTexture texture, string name)
    {
        if (texture != null && texture.width == textureSize && texture.height == textureSize)
            return;

        if (texture != null)
            texture.Release();

        texture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = $"Terrain_{name}_{textureSize}x{textureSize}",
        };

        texture.Create();

        Debug.Log($"Created {name} texture: {textureSize}x{textureSize}");
    }

    private void CreateAnalysisBuffers()
    {
        int workgroupsX = Mathf.CeilToInt(textureSize / 8.0f);
        int workgroupsY = Mathf.CeilToInt(textureSize / 8.0f);
        int totalWorkgroups = workgroupsX * workgroupsY;

        if (analysisBuffer != null) analysisBuffer.Release();
        analysisBuffer = new ComputeBuffer(totalWorkgroups, sizeof(float) * 4);

        if (globalStatsBuffer != null) globalStatsBuffer.Release();
        globalStatsBuffer = new ComputeBuffer(1, sizeof(float) * 4);
    }

    private void SaveOptimizedTextureAsPNG()
    {
        Debug.Log("Saving optimized texture as PNG...");

        // Use the highest precision format available for reading
        RenderTexture.active = optimizedTexture;

        // Create a high-precision Texture2D for reading
        // Use RGBAFloat for maximum precision, then convert to grayscale PNG
        var texture2D = new Texture2D(optimizedTexture.width, optimizedTexture.height, TextureFormat.RGBAFloat, false, true);
        texture2D.ReadPixels(new Rect(0, 0, optimizedTexture.width, optimizedTexture.height), 0, 0);
        texture2D.Apply();
        RenderTexture.active = null;

        // Convert to grayscale with maximum precision
        // PNG supports 16-bit grayscale, which gives us much better precision than 8-bit
        Color[] pixels = texture2D.GetPixels();
        var grayscalePixels = new Color[pixels.Length];

        for (var i = 0; i < pixels.Length; i++)
        {
            // Use red channel (our height data) for all RGB channels
            float height = pixels[i].r;
            grayscalePixels[i] = new Color(height, height, height, 1.0f);
        }

        // Create final texture with grayscale data
        var finalTexture = new Texture2D(optimizedTexture.width, optimizedTexture.height, TextureFormat.RGBAFloat, false, true);
        finalTexture.SetPixels(grayscalePixels);
        finalTexture.Apply();

        // Encode to PNG (Unity's PNG encoder will preserve high precision)
        byte[] pngData = finalTexture.EncodeToPNG();

        // Save to file
        string pngPath = Path.Combine(Application.dataPath, $"{saveFileName}_Optimized.png");
        File.WriteAllBytes(pngPath, pngData);

        // Cleanup
        DestroyImmediate(texture2D);
        DestroyImmediate(finalTexture);

        Debug.Log($"Optimized texture saved as PNG: {pngPath}");

#if UNITY_EDITOR

        // Auto-configure the imported PNG for linear color space and no compression
        AssetDatabase.Refresh();

        string relativePath = "Assets" + pngPath.Substring(Application.dataPath.Length);
        var importer = AssetImporter.GetAtPath(relativePath) as TextureImporter;

        if (importer != null)
        {
            importer.textureType = TextureImporterType.SingleChannel;
            importer.sRGBTexture = false; // Linear color space
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // No compression
            importer.filterMode = FilterMode.Point; // Point filtering for exact values
            importer.wrapMode = TextureWrapMode.Clamp;

            // Set max texture size to preserve resolution
            importer.maxTextureSize = Mathf.Max(textureSize, 8192);

            importer.SaveAndReimport();
            Debug.Log("PNG import settings configured for linear, uncompressed, single-channel");
        }
#endif
    }

    private void SaveBC4CompressedData()
    {
        var compressedData = new uint[compressedDataBuffer.count * 2];
        compressedDataBuffer.GetData(compressedData);

        // Create DDS file
        byte[] ddsFile = CreateBC4DDSFile(compressedData);
        string path = Path.Combine(Application.dataPath, $"{saveFileName}_BC4.dds");
        File.WriteAllBytes(path, ddsFile);

        Debug.Log($"BC4 DDS saved: {path}");
    }

    private byte[] CreateBC4DDSFile(uint[] compressedData)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            // DDS Magic
            writer.Write(0x20534444); // "DDS "

            // DDS_HEADER (124 bytes)
            writer.Write(124); // dwSize
            writer.Write(0x1 | 0x2 | 0x4 | 0x1000 | 0x80000); // dwFlags (CAPS | HEIGHT | WIDTH | PIXELFORMAT | LINEARSIZE)
            writer.Write(textureSize); // dwHeight
            writer.Write(textureSize); // dwWidth
            writer.Write(compressedData.Length * 4); // dwPitchOrLinearSize (total compressed size)
            writer.Write(0); // dwDepth
            writer.Write(0); // dwMipMapCount

            // dwReserved1[11] - 11 reserved DWORDs
            for (var i = 0; i < 11; i++)
                writer.Write(0);

            // DDS_PIXELFORMAT (32 bytes) - Use DX10 format
            writer.Write(32); // dwSize
            writer.Write(0x4); // dwFlags (FOURCC)
            writer.Write(0x30315844); // dwFourCC ("DX10")
            writer.Write(0); // dwRGBBitCount
            writer.Write(0); // dwRBitMask
            writer.Write(0); // dwGBitMask
            writer.Write(0); // dwBBitMask
            writer.Write(0); // dwABitMask

            // DDS_HEADER remaining fields
            writer.Write(0x1000); // dwCaps (TEXTURE)
            writer.Write(0); // dwCaps2
            writer.Write(0); // dwCaps3
            writer.Write(0); // dwCaps4
            writer.Write(0); // dwReserved2

            // DX10 Extended header (20 bytes)
            writer.Write(80); // DXGI_FORMAT_BC4_UNORM
            writer.Write(3); // D3D11_RESOURCE_DIMENSION_TEXTURE2D
            writer.Write(0); // miscFlag
            writer.Write(1); // arraySize
            writer.Write(0); // miscFlags2

            // CRITICAL: Handle potential Y-flipping in block data
            // BC4 blocks are stored in row-major order, but Unity might generate them differently

            int blocksX = Mathf.CeilToInt(textureSize / 4.0f);
            int blocksY = Mathf.CeilToInt(textureSize / 4.0f);

            // Option 1: Write data as-is (try this first)
            WriteCompressedDataDirect(writer, compressedData);

            // Option 2: If Y-flipped, use this instead
            // WriteCompressedDataYFlipped(writer, compressedData, blocksX, blocksY);

            return stream.ToArray();
        }
    }

    // Direct write (try this first)
    private void WriteCompressedDataDirect(BinaryWriter writer, uint[] compressedData)
    {
        // Convert uint2 array to byte array properly
        for (var i = 0; i < compressedData.Length; i += 2)
        {
            // Each BC4 block is 8 bytes (uint2 = 2 uints = 8 bytes)
            uint block0 = compressedData[i];
            uint block1 = compressedData[i + 1];

            // Write as little-endian bytes
            writer.Write(block0);
            writer.Write(block1);
        }
    }

    // Y-flipped write (try this if Option 1 doesn't work)
    private void WriteCompressedDataYFlipped(BinaryWriter writer, uint[] compressedData, int blocksX, int blocksY)
    {
        // Write blocks in Y-flipped order
        for (int blockY = blocksY - 1; blockY >= 0; blockY--) // Reverse Y order
        {
            for (var blockX = 0; blockX < blocksX; blockX++)
            {
                int originalIndex = ((blockY * blocksX) + blockX) * 2; // * 2 because uint2

                if (originalIndex + 1 < compressedData.Length)
                {
                    uint block0 = compressedData[originalIndex];
                    uint block1 = compressedData[originalIndex + 1];

                    writer.Write(block0);
                    writer.Write(block1);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (rawTexture != null) rawTexture.Release();
        if (optimizedTexture != null) optimizedTexture.Release();
        if (compressedDataBuffer != null) compressedDataBuffer.Release();
        if (analysisBuffer != null) analysisBuffer.Release();
        if (globalStatsBuffer != null) globalStatsBuffer.Release();
    }

    // Debug function
    public void DebugCurrentState()
    {
        Debug.Log("=== TERRAIN DEBUG ===");
        Debug.Log($"Raw Texture: {(rawTexture != null ? "Created" : "Null")}");
        Debug.Log($"Optimized Texture: {(optimizedTexture != null ? "Created" : "Null")}");
        Debug.Log($"Metadata Count: {_terrainMetadata.Count}");

        if (_terrainMetadata.Count > 0)
        {
            Debug.Log($"Range: [{TerrainMetadataHelper.GetOriginalMin(_terrainMetadata):F6}, {TerrainMetadataHelper.GetOriginalMax(_terrainMetadata):F6}]");
            Debug.Log($"Precision Used: {TerrainMetadataHelper.GetCompressionRatio(_terrainMetadata) * 100:F1}%");
        }
    }
}

// Simplified editor
#if UNITY_EDITOR
[CustomEditor(typeof(TerrainGeneratorWithAnalysis))]
public class TerrainGeneratorWithAnalysisEditorSimple : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        var generator = (TerrainGeneratorWithAnalysis)target;

        if (GUILayout.Button("Generate Optimized Terrain", GUILayout.Height(30))) { generator.GenerateOptimizedTerrain(); }

        GUILayout.Space(5);

        if (GUILayout.Button("Debug Current State")) { generator.DebugCurrentState(); }

        GUILayout.Space(10);

        // Show debug info
        if (generator._lastRangeWidth > 0)
        {
            GUILayout.Label("Last Analysis Results:", EditorStyles.boldLabel);
            GUILayout.Label($"Range: [{generator._lastMinHeight:F6}, {generator._lastMaxHeight:F6}]");
            GUILayout.Label($"Width: {generator._lastRangeWidth:F6}");
            GUILayout.Label($"Precision: {generator._lastPrecisionUsed:F1}%");
        }

        // Preview
        if (generator.rawTexture != null || generator.optimizedTexture != null)
        {
            GUILayout.Space(5);
            GUILayout.Label("Preview:");
            Rect previewRect = GUILayoutUtility.GetRect(200, 200);

            RenderTexture textureToShow = generator.optimizedTexture ?? generator.rawTexture;

            if (textureToShow != null) { EditorGUI.DrawPreviewTexture(previewRect, textureToShow); }
        }
    }
}
#endif
