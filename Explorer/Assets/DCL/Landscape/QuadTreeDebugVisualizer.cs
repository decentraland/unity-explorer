using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;

public class QuadTreeRenderTextureVisualizer : MonoBehaviour
{
    public struct QuadTreeNodeData
    {
        public uint Depth8CornerIndexStart24;
    }

    public struct PerInst
    {
        public half3 position;
        public ushort rotationXY;
    }

    [Header("Render Settings")]
    [Range(256, 2048)]
    public int renderTextureSize = 512;
    public RenderTexture quadTreeRenderTexture;
    public Material quadTreeRenderMaterial;

    [Header("Display Settings")]
    public bool showOverlay = true;
    [Range(0f, 1f)]
    public float overlayOpacity = 0.7f;
    [Range(0.1f, 1f)]
    public float overlayScale = 0.3f;
    public Vector2 overlayPosition = new (10, 10);

    [Header("Debug Settings")]
    public bool showQuadTreeStructure = true;
    public bool showOnlyVisibleNodes;
    public Color backgroundColor = Color.black;
    public Color nodeColor = new (0.3f, 0.3f, 0.3f, 1f);
    public Color visibleNodeColor = Color.green;
    public Color borderColor = Color.white;

    [Header("Quadtree Configuration")]
    [Range(2, 10)]
    public int maxDepth = 10;
    public Vector2 worldSize = new (8192, 8192);
    public Vector2 worldCenter = Vector2.zero;

    [Header("Camera & Compute")]
    public Camera debugCamera;
    public ComputeShader quadTreeCullingShader;
    public ComputeShader clearQuadTreeTextureShader;
    public ComputeShader renderQuadTreeTextureShader;
    public ComputeShader ScatterGrassShader;
    public Texture2D HeightMapTexture;

    // Private fields
    private readonly QuadTreeNodeData[] quadTreeNodes = new QuadTreeNodeData[349525];
    private List<int> visibleNodeIndices = new ();
    private ComputeBuffer quadTreeNodesComputeBuffer;
    private ComputeBuffer visibleParcelsComputeBuffer;
    private ComputeBuffer visibleparcelCountComputeBuffer;
    private ComputeBuffer grassInstancesComputeBuffer;
    private readonly int[] visibleCount = new int[1];
    private Camera copyCamera;

    // Rendering
    private CommandBuffer commandBuffer;
    private Material overlayMaterial;

    private void Start()
    {
        if (debugCamera == null)
            debugCamera = Camera.main;

        SetupRenderTexture();
        SetupMaterials();
        GenerateQuadTree();
        SetupComputeBuffers();
    }

    private void SetupRenderTexture()
    {
        if (quadTreeRenderTexture != null)
            quadTreeRenderTexture.Release();

        quadTreeRenderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 0, RenderTextureFormat.ARGB32);
        quadTreeRenderTexture.enableRandomWrite = true;
        quadTreeRenderTexture.Create();
    }

    private void SetupMaterials()
    {
        // Create overlay material for UI rendering
        if (overlayMaterial == null)
        {
            var overlayShader = Shader.Find("UI/Default");
            overlayMaterial = new Material(overlayShader);
        }

        // Create render material if not assigned
        if (quadTreeRenderMaterial == null)
        {
            var unlitShader = Shader.Find("Unlit/Texture");
            quadTreeRenderMaterial = new Material(unlitShader);
        }

        quadTreeRenderMaterial.mainTexture = quadTreeRenderTexture;
    }

    // Combine 8bits at top and 24bits at bottom
    public static uint CreateDepth8CornerIndexStart(byte depth, uint cornerIndexStart) =>
        ((uint)depth << 24) | cornerIndexStart;

    // Extract the top 8 bits
    public static byte GetTop8Bits(uint value) =>
        (byte)(value >> 24);

    // Extract the bottom 24 bits
    public static uint GetBottom24Bits(uint value) =>
        value & 0xFFFFFF;

    private void GenerateQuadTree()
    {
        quadTreeNodes[0].Depth8CornerIndexStart24 = 0;

        SubdivideNode(0, 0);
    }

    private void SubdivideNode(uint cornerIndexStart, byte currentDepth)
    {
        const uint nFullQuadSize = 512;
        var newDepth = (byte)(currentDepth + 1);
        uint nCornerSize = nFullQuadSize >> newDepth;
        nCornerSize *= nCornerSize;

        if (newDepth >= maxDepth)
            return;

        uint arrayPosition = 0;

        for (var layerCount = 0; layerCount < newDepth; ++layerCount)
            arrayPosition += (uint)(1 << (layerCount * 2));

        var cornerIndexStartArray = new uint[4];

        // NW - Top Left
        uint nodeIndex_NW = arrayPosition + (uint)Mathf.FloorToInt((float)cornerIndexStart / nCornerSize);
        quadTreeNodes[nodeIndex_NW].Depth8CornerIndexStart24 = CreateDepth8CornerIndexStart(newDepth, cornerIndexStart);
        cornerIndexStartArray[0] = cornerIndexStart;

        // NE - Top Right
        uint nodeIndex_NE = arrayPosition + 1 + (uint)Mathf.FloorToInt((float)cornerIndexStart / nCornerSize);
        quadTreeNodes[nodeIndex_NE].Depth8CornerIndexStart24 = CreateDepth8CornerIndexStart(newDepth, cornerIndexStart + (nCornerSize * 1));
        cornerIndexStartArray[1] = cornerIndexStart + (nCornerSize * 1);

        // SW - Bottom Left
        uint nodeIndex_SW = arrayPosition + 2 + (uint)Mathf.FloorToInt((float)cornerIndexStart / nCornerSize);
        quadTreeNodes[nodeIndex_SW].Depth8CornerIndexStart24 = CreateDepth8CornerIndexStart(newDepth, cornerIndexStart + (nCornerSize * 2));
        cornerIndexStartArray[2] = cornerIndexStart + (nCornerSize * 2);

        // SE - Bottom Right
        uint nodeIndex_SE = arrayPosition + 3 + (uint)Mathf.FloorToInt((float)cornerIndexStart / nCornerSize);
        quadTreeNodes[nodeIndex_SE].Depth8CornerIndexStart24 = CreateDepth8CornerIndexStart(newDepth, cornerIndexStart + (nCornerSize * 3));
        cornerIndexStartArray[3] = cornerIndexStart + (nCornerSize * 3);

        for (byte i = 0; i < 4; ++i) { SubdivideNode(cornerIndexStartArray[i], newDepth); }
    }

    private void SetupComputeBuffers()
    {
        ReleaseBuffers();

        if (!quadTreeNodes.Any())
            return;

        quadTreeNodesComputeBuffer = new ComputeBuffer(quadTreeNodes.Length, Marshal.SizeOf<QuadTreeNodeData>());
        visibleParcelsComputeBuffer = new ComputeBuffer(512 * 512, sizeof(int) * 2);
        visibleparcelCountComputeBuffer = new ComputeBuffer(1, sizeof(int));
        grassInstancesComputeBuffer = new ComputeBuffer(256 * 256, Marshal.SizeOf<PerInst>());

        quadTreeNodesComputeBuffer.SetData(quadTreeNodes.ToArray());
    }

    private void Update()
    {
        RunFrustumCulling();
        RenderQuadTreeToTexture();
        GenerateScatteredGrass();
    }

    private void RunFrustumCulling()
    {
        if (quadTreeCullingShader == null || debugCamera == null || quadTreeNodesComputeBuffer == null)
            return;

        // Reset visible count
        visibleCount[0] = 0;
        visibleparcelCountComputeBuffer.SetData(visibleCount);

        // Set up compute shader
        int kernelIndex = quadTreeCullingShader.FindKernel("HierarchicalQuadTreeCulling");

        // Set camera data
        if (copyCamera == null)
        {
            GameObject cameraCopy_GO = Instantiate(debugCamera.gameObject);
            copyCamera = cameraCopy_GO.GetComponent<Camera>();
        }

        copyCamera.CopyFrom(debugCamera);
        copyCamera.farClipPlane = 256;
        Matrix4x4 viewMatrix = copyCamera.worldToCameraMatrix;
        Matrix4x4 projMatrix = copyCamera.projectionMatrix;
        Matrix4x4 viewProjMatrix = projMatrix * viewMatrix;

        quadTreeCullingShader.SetMatrix("viewMatrix", viewMatrix);
        quadTreeCullingShader.SetMatrix("projMatrix", projMatrix);
        quadTreeCullingShader.SetMatrix("viewProjMatrix", viewProjMatrix);
        quadTreeCullingShader.SetVector("cameraPosition", copyCamera.transform.position);
        quadTreeCullingShader.SetFloat("nearPlane", copyCamera.nearClipPlane);
        quadTreeCullingShader.SetFloat("farPlane", copyCamera.farClipPlane);
        quadTreeCullingShader.SetVector("cameraForward", copyCamera.transform.forward);
        quadTreeCullingShader.SetFloat("fov", copyCamera.fieldOfView);
        quadTreeCullingShader.SetVector("cameraUp", copyCamera.transform.up);
        quadTreeCullingShader.SetFloat("aspectRatio", copyCamera.aspect);
        quadTreeCullingShader.SetVector("cameraRight", copyCamera.transform.right);

        quadTreeCullingShader.SetBuffer(kernelIndex, "quadTreeNodes", quadTreeNodesComputeBuffer);
        quadTreeCullingShader.SetBuffer(kernelIndex, "visibleParcels", visibleParcelsComputeBuffer);
        quadTreeCullingShader.SetBuffer(kernelIndex, "visibleParcelCount", visibleparcelCountComputeBuffer);

        int threadGroups = Mathf.CeilToInt((quadTreeNodes.Length - 87381) / 256.0f);
        quadTreeCullingShader.Dispatch(kernelIndex, threadGroups, 1, 1);

        // Read back results
        visibleparcelCountComputeBuffer.GetData(visibleCount);
    }

    private void RenderQuadTreeToTexture()
    {
        if (renderQuadTreeTextureShader == null || quadTreeRenderTexture == null || quadTreeNodesComputeBuffer == null)
            return;

        int kernelIndex = clearQuadTreeTextureShader.FindKernel("ClearTexture");

        // Set parameters
        clearQuadTreeTextureShader.SetTexture(kernelIndex, "ResultTexture", quadTreeRenderTexture);
        clearQuadTreeTextureShader.Dispatch(kernelIndex, 512 / 8, 512 / 8, 1);

        kernelIndex = renderQuadTreeTextureShader.FindKernel("SetVisiblePixels");
        renderQuadTreeTextureShader.SetTexture(kernelIndex, "ResultTexture", quadTreeRenderTexture);
        renderQuadTreeTextureShader.SetBuffer(kernelIndex, "visibleNodes", visibleParcelsComputeBuffer);
        renderQuadTreeTextureShader.SetBuffer(kernelIndex, "visibleParcelCount", visibleparcelCountComputeBuffer);
        renderQuadTreeTextureShader.Dispatch(kernelIndex, (262144 + 63) / 64, 1, 1);
    }

    private void GenerateScatteredGrass()
    {
        if (ScatterGrassShader == null)
            return;

        // Set up compute shader
        int kernelIndex = ScatterGrassShader.FindKernel("ScatterGrass");

        ScatterGrassShader.SetTexture(kernelIndex, "HeightMapTexture", HeightMapTexture);
        ScatterGrassShader.SetBuffer(kernelIndex, "visibleParcels", visibleParcelsComputeBuffer);
        ScatterGrassShader.SetBuffer(kernelIndex, "visibleParcelCount", visibleparcelCountComputeBuffer);
        ScatterGrassShader.SetBuffer(kernelIndex, "instances", grassInstancesComputeBuffer);

        int threadGroups = Mathf.CeilToInt(256);
        ScatterGrassShader.Dispatch(kernelIndex, threadGroups, 1, 1);
    }

    private void OnGUI()
    {
        if (!showOverlay || quadTreeRenderTexture == null)
            return;

        // Calculate overlay size and position
        float overlayWidth = Screen.width * overlayScale;
        float overlayHeight = Screen.height * overlayScale;

        var overlayRect = new Rect(
            overlayPosition.x,
            overlayPosition.y,
            overlayWidth,
            overlayHeight
        );

        // Set overlay opacity
        Color oldColor = GUI.color;
        GUI.color = new Color(1, 1, 1, overlayOpacity);

        // Draw the render texture
        GUI.DrawTexture(overlayRect, quadTreeRenderTexture);

        // Draw border
        GUI.color = Color.white;
        GUI.Box(overlayRect, "");

        // Draw info text
        GUILayout.BeginArea(new Rect(overlayRect.x, overlayRect.y + overlayRect.height + 5, overlayRect.width, 100));
        GUILayout.BeginVertical();

        var smallText = new GUIStyle(GUI.skin.label);
        smallText.fontSize = 10;

        GUILayout.Label($"Nodes: {quadTreeNodes.Length}", smallText);
        GUILayout.Label($"Visible: {visibleCount[0]}", smallText);

        if (quadTreeNodes.Any())
        {
            float efficiency = (1f - ((float)visibleCount[0] / quadTreeNodes.Length)) * 100f;
            GUILayout.Label($"Culled: {efficiency:F1}%", smallText);
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();

        // Controls info
        GUILayout.BeginArea(new Rect(10, Screen.height - 60, 300, 60));
        GUILayout.BeginVertical("box");
        GUILayout.Label("Quadtree Frustum Culling Visualization", smallText);
        GUILayout.Label("Real-time updates as camera moves", smallText);
        GUILayout.EndVertical();
        GUILayout.EndArea();

        GUI.color = oldColor;
    }

    private void OnDestroy()
    {
        ReleaseBuffers();

        if (quadTreeRenderTexture != null) { quadTreeRenderTexture.Release(); }
    }

    private void ReleaseBuffers()
    {
        quadTreeNodesComputeBuffer?.Release();
        visibleParcelsComputeBuffer?.Release();
        visibleparcelCountComputeBuffer?.Release();
    }
}
