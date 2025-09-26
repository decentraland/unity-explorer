using DCL.Landscape;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Decentraland.Terrain
{
    [CreateAssetMenu]
    public sealed class GrassIndirectRenderer : ScriptableObject
    {
        [field: SerializeField] private ComputeShader QuadTreeCullingShader { get; set; }
        [field: SerializeField] private ComputeShader ScatterGrassShader { get; set; }
        [field: SerializeField] private ComputeShader ScatterFlowersShader { get; set; }
        [field: SerializeField] private ComputeShader ScatterCatTailsShader { get; set; }
        [field: SerializeField] private Texture2D HeightMapTexture { get; set; }
        [field: SerializeField] private Texture2D TerrainBlendTexture { get; set; }
        [field: SerializeField] private Texture2D GroundDetailTexture { get; set; }
        [field: SerializeField] private Texture2D SandDetailTexture { get; set; }

        // Cached shader property IDs
        private static class ShaderProperties
        {
            // QuadTreeCulling properties
            public static readonly int ViewProjMatrix = Shader.PropertyToID("viewProjMatrix");
            public static readonly int TerrainBounds = Shader.PropertyToID("TerrainBounds");
            public static readonly int FloorValue = Shader.PropertyToID("floorValue");
            public static readonly int OccupancyTexture = Shader.PropertyToID("OccupancyTexture");
            public static readonly int OccupancyMapSize = Shader.PropertyToID("occupancyMapSize");
            public static readonly int QuadTreeNodes = Shader.PropertyToID("quadTreeNodes");
            public static readonly int VisibleParcels = Shader.PropertyToID("visibleParcels");
            public static readonly int VisibleParcelCount = Shader.PropertyToID("visibleParcelCount");

            // ScatterGrass properties
            public static readonly int TerrainHeight = Shader.PropertyToID("TerrainHeight");
            public static readonly int MinDistOccupancy = Shader.PropertyToID("_MinDistOccupancy");
            public static readonly int HeightMapTexture = Shader.PropertyToID("HeightMapTexture");
            public static readonly int TerrainBlendTexture = Shader.PropertyToID("TerrainBlendTexture");
            public static readonly int GroundDetailTexture = Shader.PropertyToID("GroundDetailTexture");
            public static readonly int SandDetailTexture = Shader.PropertyToID("SandDetailTexture");
            public static readonly int GrassInstances = Shader.PropertyToID("grassInstances");
            public static readonly int DrawArgs = Shader.PropertyToID("drawArgs");

            // ScatterFlowers properties
            public static readonly int NThreads = Shader.PropertyToID("nThreads");
            public static readonly int HeightTextureSize = Shader.PropertyToID("HeightTextureSize");
            public static readonly int OccupancyTextureSize = Shader.PropertyToID("OccupancyTextureSize");
            public static readonly int TerrainBlendTextureSize = Shader.PropertyToID("TerrainBlendTextureSize");
            public static readonly int GroundDetailTextureSize = Shader.PropertyToID("GroundDetailTextureSize");
            public static readonly int SandDetailTextureSize = Shader.PropertyToID("SandDetailTextureSize");
            public static readonly int ParcelSize = Shader.PropertyToID("parcelSize");
            public static readonly int FDistanceFieldScale = Shader.PropertyToID("fDistanceFieldScale");
            public static readonly int NHeightMapSize = Shader.PropertyToID("nHeightMapSize");
            public static readonly int FSplatMapTiling = Shader.PropertyToID("fSplatMapTiling");
            public static readonly int Flower0Instances = Shader.PropertyToID("flower0Instances");
            public static readonly int Flower1Instances = Shader.PropertyToID("flower1Instances");
            public static readonly int Flower2Instances = Shader.PropertyToID("flower2Instances");
            public static readonly int DrawArgs0 = Shader.PropertyToID("drawArgs0");
            public static readonly int DrawArgs1 = Shader.PropertyToID("drawArgs1");
            public static readonly int DrawArgs2 = Shader.PropertyToID("drawArgs2");

            // Material properties
            public static readonly int PerParcelBuffer = Shader.PropertyToID("_PerParcelBuffer");
            public static readonly int PerInstanceBuffer = Shader.PropertyToID("_PerInstanceBuffer");
            public static readonly int ColorMapParams = Shader.PropertyToID("_ColorMapParams");
            public static readonly int BatchingBlockSize = Shader.PropertyToID("_batchingBlockSize");

            // Global wind property
            public static readonly int Wind = Shader.PropertyToID("_Wind");
        }

        private static class ShaderKernels
        {
            public static int ScatterGrassKernel;
            public static int ScatterFlowersKernel;
            public static int QuadTreeCullingKernel;
            public static int ScatterCatTailsShader;
        }

        public struct QuadTreeNodeData
        {
            public uint Depth8CornerIndexStart24;
        }

        public struct PerInst
        {
            public float4 position;
            public float4 quatRotation;
            public float4 colour;
        }

        private readonly int[] arrLOD = new int[3] { 0, 0, 0 };
        private int[] arrLODPull = new int[3] { 0, 0, 0 };

        //private int renderTextureSize = 512;
        private readonly int maxDepth = 10;
        [NonSerialized] private bool initialized;
        private readonly QuadTreeNodeData[] quadTreeNodes = new QuadTreeNodeData[349525];
        private ComputeBuffer quadTreeNodesComputeBuffer;
        private ComputeBuffer visibleParcelsComputeBuffer;
        private ComputeBuffer visibleparcelCountComputeBuffer;
        private ComputeBuffer grassInstancesComputeBuffer;
        private GraphicsBuffer arrInstCount;
        private ComputeBuffer flower0InstancesComputeBuffer;
        private ComputeBuffer flower1InstancesComputeBuffer;
        private ComputeBuffer flower2InstancesComputeBuffer;
        private GraphicsBuffer grassDrawArgs;
        private GraphicsBuffer flower0DrawArgs;
        private GraphicsBuffer flower1DrawArgs;
        private GraphicsBuffer flower2DrawArgs;
        private readonly uint[] grassArgs = new uint[5];
        private readonly uint[] flower0Args = new uint[5];
        private readonly uint[] flower1Args = new uint[5];
        private readonly uint[] flower2Args = new uint[5];
        private readonly int[] visibleCount = new int[1];
        private Mesh grassMesh;
        private Material grassMaterial;
        private Mesh flower0Mesh;
        private Material flower0Material;
        private Mesh flower1Mesh;
        private Material flower1Material;
        private Mesh flower2Mesh;
        private Material flower2Material;
        private Bounds indirectRenderingBounds;

        private readonly int parcelSize = 16;
        private readonly float nHeightMapSize = 8192.0f;
        private readonly float fSplatMapTiling = 8.0f;

        private readonly int grassInstancesPerParcel = 256;
        private readonly int flowerInstancesPerParcel = 16;
        private readonly int maxVisibleParcels = 256;
        private readonly int parcelGridSingleAxisSize = 512;

        private float fDistanceFieldScale;

        private MaterialPropertyBlock grassMaterialPropertyBlock;
        private MaterialPropertyBlock flowersMaterialPropertyBlock;

        public static uint CreateDepth8CornerIndexStart(byte depth, uint cornerIndexStart) =>
            ((uint)depth << 24) | cornerIndexStart;

        private void Initialize(ITerrain terrainGenerator)
        {
            if (initialized)
                return;

            initialized = true;

            GenerateQuadTree();
            SetupComputeBuffers();


            ShaderKernels.ScatterGrassKernel = ScatterGrassShader.FindKernel("ScatterGrass");
            ShaderKernels.ScatterFlowersKernel = ScatterFlowersShader.FindKernel("FlowerScatter");
            ShaderKernels.QuadTreeCullingKernel = QuadTreeCullingShader.FindKernel("HierarchicalQuadTreeCulling");
            ShaderKernels.ScatterCatTailsShader = ScatterCatTailsShader.FindKernel("CatTailScatter");

            grassMaterialPropertyBlock = new MaterialPropertyBlock();
            flowersMaterialPropertyBlock = new MaterialPropertyBlock();
        }

        public void OnTerrainLoaded(ITerrain terrain)
        {
            Initialize(terrain);

            TerrainModel model = terrain.TerrainModel!;
            int2 min = model.MinInUnits;
            int2 max = model.MaxInUnits;

            fDistanceFieldScale = terrain.MaxHeight;
            indirectRenderingBounds.SetMinMax(new Vector3(min.x, 0f, min.y),
                new Vector3(max.x, terrain.MaxHeight, max.y));
        }

        public void Render(LandscapeData landscapeData, ITerrain terrainGenerator,
            Camera camera, bool renderToAllCameras)
        {
            RunFrustumCulling(landscapeData, terrainGenerator, camera);
            GenerateScatteredGrass(terrainGenerator);
            arrInstCount.SetData(arrLOD, 0, 0, 3);
            GenerateScatteredFlowers(terrainGenerator);
            GenerateScatteredCatTails(terrainGenerator);

            LandscapeAsset grass = landscapeData.terrainData.detailAssets[0];
            SetGrassMeshAndMaterial(grass.TerrainDetailSettings.Mesh, grass.TerrainDetailSettings.Material);
            RenderGrass(renderToAllCameras ? null : camera);

            LandscapeAsset flower0 = landscapeData.terrainData.detailAssets[1];
            SetFlower0MeshAndMaterial(flower0.TerrainDetailSettings.Mesh, flower0.TerrainDetailSettings.Material);

            LandscapeAsset flower1 = landscapeData.terrainData.detailAssets[2];
            SetFlower1MeshAndMaterial(flower1.TerrainDetailSettings.Mesh, flower1.TerrainDetailSettings.Material);

            LandscapeAsset flower2 = landscapeData.terrainData.detailAssets[3];
            SetFlower2MeshAndMaterial(flower2.TerrainDetailSettings.Mesh, flower2.TerrainDetailSettings.Material);
            RenderFlowers(renderToAllCameras ? null : camera);
        }

        public void SetGrassMeshAndMaterial(Mesh mesh, Material material)
        {
            grassMesh = mesh;
            grassMaterial = material;
            grassMaterial.EnableKeyword("_GPU_GRASS_BATCHING");
            grassArgs[0] = grassMesh.GetIndexCount(0); // indexCountPerInstance
            grassArgs[1] = 0; // instanceCount
            grassArgs[2] = grassMesh.GetIndexStart(0); // startIndexLocation
            grassArgs[3] = grassMesh.GetBaseVertex(0); // baseVertexLocation
            grassArgs[4] = 0; // startInstanceLocation
        }

        public void SetFlower0MeshAndMaterial(Mesh mesh, Material material)
        {
            flower0Mesh = mesh;
            flower0Material = material;
            flower0Material.EnableKeyword("_GPU_GRASS_BATCHING");
            flower0Args[0] = flower0Mesh.GetIndexCount(0); // indexCountPerInstance
            flower0Args[1] = 0; // instanceCount
            flower0Args[2] = flower0Mesh.GetIndexStart(0); // startIndexLocation
            flower0Args[3] = flower0Mesh.GetBaseVertex(0); // baseVertexLocation
            flower0Args[4] = 0; // startInstanceLocation
        }

        public void SetFlower1MeshAndMaterial(Mesh mesh, Material material)
        {
            flower1Mesh = mesh;
            flower1Material = material;
            flower1Material.EnableKeyword("_GPU_GRASS_BATCHING");
            flower1Args[0] = flower1Mesh.GetIndexCount(0); // indexCountPerInstance
            flower1Args[1] = 0; // instanceCount
            flower1Args[2] = flower1Mesh.GetIndexStart(0); // startIndexLocation
            flower1Args[3] = flower1Mesh.GetBaseVertex(0); // baseVertexLocation
            flower1Args[4] = 0; // startInstanceLocation
        }

        public void SetFlower2MeshAndMaterial(Mesh mesh, Material material)
        {
            flower2Mesh = mesh;
            flower2Material = material;
            flower2Material.EnableKeyword("_GPU_GRASS_BATCHING");
            flower2Args[0] = flower2Mesh.GetIndexCount(0); // indexCountPerInstance
            flower2Args[1] = 0; // instanceCount
            flower2Args[2] = flower2Mesh.GetIndexStart(0); // startIndexLocation
            flower2Args[3] = flower2Mesh.GetBaseVertex(0); // baseVertexLocation
            flower2Args[4] = 0; // startInstanceLocation
        }

        public void GenerateQuadTree()
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

            quadTreeNodes[nodeIndex_NW].Depth8CornerIndexStart24 =
                CreateDepth8CornerIndexStart(newDepth, cornerIndexStart);

            cornerIndexStartArray[0] = cornerIndexStart;

            // NE - Top Right
            uint nodeIndex_NE = arrayPosition + 1 + (uint)Mathf.FloorToInt((float)cornerIndexStart / nCornerSize);

            quadTreeNodes[nodeIndex_NE].Depth8CornerIndexStart24 =
                CreateDepth8CornerIndexStart(newDepth, cornerIndexStart + (nCornerSize * 1));

            cornerIndexStartArray[1] = cornerIndexStart + (nCornerSize * 1);

            // SW - Bottom Left
            uint nodeIndex_SW = arrayPosition + 2 + (uint)Mathf.FloorToInt((float)cornerIndexStart / nCornerSize);

            quadTreeNodes[nodeIndex_SW].Depth8CornerIndexStart24 =
                CreateDepth8CornerIndexStart(newDepth, cornerIndexStart + (nCornerSize * 2));

            cornerIndexStartArray[2] = cornerIndexStart + (nCornerSize * 2);

            // SE - Bottom Right
            uint nodeIndex_SE = arrayPosition + 3 + (uint)Mathf.FloorToInt((float)cornerIndexStart / nCornerSize);

            quadTreeNodes[nodeIndex_SE].Depth8CornerIndexStart24 =
                CreateDepth8CornerIndexStart(newDepth, cornerIndexStart + (nCornerSize * 3));

            cornerIndexStartArray[3] = cornerIndexStart + (nCornerSize * 3);

            for (byte i = 0; i < 4; ++i) { SubdivideNode(cornerIndexStartArray[i], newDepth); }
        }

        public void SetupComputeBuffers()
        {
            ReleaseBuffers();

            if (!quadTreeNodes.Any())
                return;

            quadTreeNodesComputeBuffer = new ComputeBuffer(quadTreeNodes.Length, Marshal.SizeOf<QuadTreeNodeData>());
            visibleParcelsComputeBuffer = new ComputeBuffer(parcelGridSingleAxisSize * parcelGridSingleAxisSize, sizeof(int) * 2);
            visibleparcelCountComputeBuffer = new ComputeBuffer(1, sizeof(int));
            grassInstancesComputeBuffer = new ComputeBuffer(grassInstancesPerParcel * maxVisibleParcels, Marshal.SizeOf<PerInst>());
            flower0InstancesComputeBuffer = new ComputeBuffer(flowerInstancesPerParcel * maxVisibleParcels, Marshal.SizeOf<PerInst>());
            flower1InstancesComputeBuffer = new ComputeBuffer(flowerInstancesPerParcel * maxVisibleParcels, Marshal.SizeOf<PerInst>());
            flower2InstancesComputeBuffer = new ComputeBuffer(flowerInstancesPerParcel * maxVisibleParcels, Marshal.SizeOf<PerInst>());
            arrInstCount = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, 3, sizeof(uint));
            grassDrawArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, count: 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            flower0DrawArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, count: 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            flower1DrawArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, count: 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            flower2DrawArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, count: 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);

            quadTreeNodesComputeBuffer.SetData(quadTreeNodes.ToArray());
        }

        private void RunFrustumCulling(LandscapeData landscapeData, ITerrain terrainGenerator,
            Camera camera)
        {
            if (QuadTreeCullingShader == null ||
                quadTreeNodesComputeBuffer == null ||
                visibleParcelsComputeBuffer == null ||
                visibleparcelCountComputeBuffer == null)
                return;

            // Reset visible count
            visibleCount[0] = 0;
            visibleparcelCountComputeBuffer.SetData(visibleCount);

            // Set up compute shader

            // Set camera data
            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;

            var projMatrix = Matrix4x4.Perspective(camera.fieldOfView, camera.aspect,
                camera.nearClipPlane, Mathf.Clamp(Mathf.Min(camera.farClipPlane, landscapeData.DetailDistance), 0.0f, 180.0f));

            Matrix4x4 viewProjMatrix = projMatrix * viewMatrix;

            var terrainBounds = new Vector4(terrainGenerator.TerrainModel.MinParcel.x, terrainGenerator.TerrainModel.MaxParcel.x + 1,
                terrainGenerator.TerrainModel.MinParcel.y, terrainGenerator.TerrainModel.MaxParcel.y + 1);

            QuadTreeCullingShader.SetMatrix(ShaderProperties.ViewProjMatrix, viewProjMatrix);
            QuadTreeCullingShader.SetVector(ShaderProperties.TerrainBounds, terrainBounds);
            QuadTreeCullingShader.SetFloat(ShaderProperties.FloorValue, terrainGenerator.OccupancyFloor / 255f);
            QuadTreeCullingShader.SetInt(ShaderProperties.OccupancyMapSize, terrainGenerator.OccupancyMapSize);
            QuadTreeCullingShader.SetInt(ShaderProperties.ParcelSize, parcelSize);

            QuadTreeCullingShader.SetTexture(ShaderKernels.QuadTreeCullingKernel, ShaderProperties.OccupancyTexture,
                terrainGenerator.OccupancyMap != null ? terrainGenerator.OccupancyMap : Texture2D.blackTexture);

            QuadTreeCullingShader.SetBuffer(ShaderKernels.QuadTreeCullingKernel, ShaderProperties.QuadTreeNodes, quadTreeNodesComputeBuffer);
            QuadTreeCullingShader.SetBuffer(ShaderKernels.QuadTreeCullingKernel, ShaderProperties.VisibleParcels, visibleParcelsComputeBuffer);
            QuadTreeCullingShader.SetBuffer(ShaderKernels.QuadTreeCullingKernel, ShaderProperties.VisibleParcelCount, visibleparcelCountComputeBuffer);

            int threadGroups = Mathf.CeilToInt((quadTreeNodes.Length - 87381) / 256.0f);
            QuadTreeCullingShader.Dispatch(ShaderKernels.QuadTreeCullingKernel, threadGroups, 1, 1);
        }

        private void GenerateScatteredGrass(ITerrain terrainGenerator)
        {
            if (ScatterGrassShader == null ||
                HeightMapTexture == null ||
                visibleParcelsComputeBuffer == null ||
                visibleparcelCountComputeBuffer == null ||
                grassInstancesComputeBuffer == null ||
                TerrainBlendTexture == null)
                return;

            grassDrawArgs.SetData(grassArgs);

            // Set up compute shader (refresh constants every dispatch)
            var terrainBounds = new Vector4(terrainGenerator.TerrainModel.MinParcel.x, terrainGenerator.TerrainModel.MaxParcel.x + 1,
                terrainGenerator.TerrainModel.MinParcel.y, terrainGenerator.TerrainModel.MaxParcel.y + 1);

            var HeightTextureSize = new int2(8192, 8192);
            //var OccupancyTextureSize = new int2(512, 512);
            var TerrainBlendTextureSize = new int2(1024, 1024);
            var GroundDetailTextureSize = new int2(2048, 2048);
            var SandDetailTextureSize = new int2(1024, 1024);

            ScatterGrassShader.SetInt2(ShaderProperties.HeightTextureSize, HeightTextureSize.x, HeightTextureSize.y);
            int OccupancyHeightWidth = terrainGenerator.OccupancyMap != null ? terrainGenerator.OccupancyMapSize : Texture2D.blackTexture.height;
            ScatterGrassShader.SetInt2(ShaderProperties.OccupancyTextureSize, OccupancyHeightWidth, OccupancyHeightWidth);
            ScatterGrassShader.SetInt2(ShaderProperties.TerrainBlendTextureSize, TerrainBlendTextureSize.x, TerrainBlendTextureSize.y);
            ScatterGrassShader.SetInt2(ShaderProperties.GroundDetailTextureSize, GroundDetailTextureSize.x, GroundDetailTextureSize.y);
            ScatterGrassShader.SetInt2(ShaderProperties.SandDetailTextureSize, SandDetailTextureSize.x, SandDetailTextureSize.y);

            ScatterGrassShader.SetVector(ShaderProperties.TerrainBounds, terrainBounds);
            ScatterGrassShader.SetFloat(ShaderProperties.TerrainHeight, terrainGenerator.MaxHeight);
            ScatterGrassShader.SetInt(ShaderProperties.ParcelSize, parcelSize);
            ScatterGrassShader.SetFloat(ShaderProperties.FDistanceFieldScale, fDistanceFieldScale);
            ScatterGrassShader.SetFloat(ShaderProperties.NHeightMapSize, nHeightMapSize);
            ScatterGrassShader.SetFloat(ShaderProperties.FSplatMapTiling, fSplatMapTiling);
            ScatterGrassShader.SetFloat(ShaderProperties.MinDistOccupancy, terrainGenerator.OccupancyFloor / 255.0f);
            ScatterGrassShader.SetTexture(ShaderKernels.ScatterGrassKernel, ShaderProperties.HeightMapTexture, HeightMapTexture);
            ScatterGrassShader.SetTexture(ShaderKernels.ScatterGrassKernel, ShaderProperties.TerrainBlendTexture, TerrainBlendTexture);
            ScatterGrassShader.SetTexture(ShaderKernels.ScatterGrassKernel, ShaderProperties.GroundDetailTexture, GroundDetailTexture);
            ScatterGrassShader.SetTexture(ShaderKernels.ScatterGrassKernel, ShaderProperties.SandDetailTexture, SandDetailTexture);

            ScatterGrassShader.SetTexture(ShaderKernels.ScatterGrassKernel, ShaderProperties.OccupancyTexture,
                terrainGenerator.OccupancyMap != null ? terrainGenerator.OccupancyMap : Texture2D.blackTexture);

            ScatterGrassShader.SetBuffer(ShaderKernels.ScatterGrassKernel, ShaderProperties.VisibleParcels, visibleParcelsComputeBuffer);
            ScatterGrassShader.SetBuffer(ShaderKernels.ScatterGrassKernel, ShaderProperties.VisibleParcelCount, visibleparcelCountComputeBuffer);
            ScatterGrassShader.SetBuffer(ShaderKernels.ScatterGrassKernel, ShaderProperties.GrassInstances, grassInstancesComputeBuffer);
            ScatterGrassShader.SetBuffer(ShaderKernels.ScatterGrassKernel, ShaderProperties.DrawArgs, grassDrawArgs);

            uint threadGroupSizes_X, threadGroupSizes_Y, threadGroupSizes_Z;

            ScatterGrassShader.GetKernelThreadGroupSizes(ShaderKernels.ScatterGrassKernel, out threadGroupSizes_X, out threadGroupSizes_Y,
                out threadGroupSizes_Z);

            ScatterGrassShader.Dispatch(ShaderKernels.ScatterGrassKernel,
                Mathf.CeilToInt(grassInstancesPerParcel * maxVisibleParcels / threadGroupSizes_X),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Y),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Z));
        }

        private void GenerateScatteredFlowers(ITerrain terrainGenerator)
        {
            if (ScatterFlowersShader == null ||
                HeightMapTexture == null ||
                visibleParcelsComputeBuffer == null ||
                visibleparcelCountComputeBuffer == null ||
                grassInstancesComputeBuffer == null ||
                TerrainBlendTexture == null)
                return;

            flower0DrawArgs.SetData(flower0Args);
            flower1DrawArgs.SetData(flower1Args);

            // Set up compute shader (refresh constants every dispatch)
            var terrainBounds = new Vector4(terrainGenerator.TerrainModel.MinParcel.x, terrainGenerator.TerrainModel.MaxParcel.x + 1,
                terrainGenerator.TerrainModel.MinParcel.y, terrainGenerator.TerrainModel.MaxParcel.y + 1);

            var HeightTextureSize = new int2(8192, 8192);
            //var OccupancyTextureSize = new int2(512, 512);
            var TerrainBlendTextureSize = new int2(1024, 1024);
            var GroundDetailTextureSize = new int2(2048, 2048);
            var SandDetailTextureSize = new int2(1024, 1024);

            ScatterFlowersShader.EnableKeyword("THREADS_16");
            ScatterFlowersShader.SetInt(ShaderProperties.NThreads, flowerInstancesPerParcel);
            ScatterFlowersShader.SetVector(ShaderProperties.TerrainBounds, terrainBounds);
            ScatterFlowersShader.SetFloat(ShaderProperties.TerrainHeight, terrainGenerator.MaxHeight);

            ScatterFlowersShader.SetInt2(ShaderProperties.HeightTextureSize, HeightTextureSize.x, HeightTextureSize.y);
            int OccupancyHeightWidth = terrainGenerator.OccupancyMap != null ? terrainGenerator.OccupancyMapSize : Texture2D.blackTexture.height;
            ScatterFlowersShader.SetInt2(ShaderProperties.OccupancyTextureSize, OccupancyHeightWidth, OccupancyHeightWidth);
            ScatterFlowersShader.SetInt2(ShaderProperties.TerrainBlendTextureSize, TerrainBlendTextureSize.x, TerrainBlendTextureSize.y);
            ScatterFlowersShader.SetInt2(ShaderProperties.GroundDetailTextureSize, GroundDetailTextureSize.x, GroundDetailTextureSize.y);
            ScatterFlowersShader.SetInt2(ShaderProperties.SandDetailTextureSize, SandDetailTextureSize.x, SandDetailTextureSize.y);

            ScatterFlowersShader.SetInt(ShaderProperties.ParcelSize, parcelSize);
            ScatterFlowersShader.SetFloat(ShaderProperties.FDistanceFieldScale, fDistanceFieldScale);
            ScatterFlowersShader.SetFloat(ShaderProperties.NHeightMapSize, nHeightMapSize);
            ScatterFlowersShader.SetFloat(ShaderProperties.FSplatMapTiling, fSplatMapTiling);
            ScatterFlowersShader.SetFloat(ShaderProperties.MinDistOccupancy, terrainGenerator.OccupancyFloor / 255f);
            ScatterFlowersShader.SetTexture(ShaderKernels.ScatterFlowersKernel, ShaderProperties.HeightMapTexture, HeightMapTexture);
            ScatterFlowersShader.SetTexture(ShaderKernels.ScatterFlowersKernel, ShaderProperties.TerrainBlendTexture, TerrainBlendTexture);
            ScatterFlowersShader.SetTexture(ShaderKernels.ScatterFlowersKernel, ShaderProperties.GroundDetailTexture, GroundDetailTexture);
            ScatterFlowersShader.SetTexture(ShaderKernels.ScatterFlowersKernel, ShaderProperties.SandDetailTexture, SandDetailTexture);

            ScatterFlowersShader.SetTexture(ShaderKernels.ScatterFlowersKernel, ShaderProperties.OccupancyTexture,
                terrainGenerator.OccupancyMap != null ? terrainGenerator.OccupancyMap : Texture2D.blackTexture);

            ScatterFlowersShader.SetBuffer(ShaderKernels.ScatterFlowersKernel, ShaderProperties.VisibleParcels, visibleParcelsComputeBuffer);
            ScatterFlowersShader.SetBuffer(ShaderKernels.ScatterFlowersKernel, ShaderProperties.VisibleParcelCount, visibleparcelCountComputeBuffer);
            ScatterFlowersShader.SetBuffer(ShaderKernels.ScatterFlowersKernel, ShaderProperties.Flower0Instances, flower0InstancesComputeBuffer);
            ScatterFlowersShader.SetBuffer(ShaderKernels.ScatterFlowersKernel, ShaderProperties.Flower1Instances, flower1InstancesComputeBuffer);
            ScatterFlowersShader.SetBuffer(ShaderKernels.ScatterFlowersKernel, ShaderProperties.DrawArgs0, flower0DrawArgs);
            ScatterFlowersShader.SetBuffer(ShaderKernels.ScatterFlowersKernel, ShaderProperties.DrawArgs1, flower1DrawArgs);

            uint threadGroupSizes_X, threadGroupSizes_Y, threadGroupSizes_Z;
            ScatterFlowersShader.GetKernelThreadGroupSizes(ShaderKernels.ScatterFlowersKernel, out threadGroupSizes_X, out threadGroupSizes_Y, out threadGroupSizes_Z);

            ScatterFlowersShader.Dispatch(ShaderKernels.ScatterFlowersKernel,
                Mathf.CeilToInt(flowerInstancesPerParcel * maxVisibleParcels / threadGroupSizes_X),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Y),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Z));
        }

        private void GenerateScatteredCatTails(ITerrain terrainGenerator)
        {
            if (ScatterCatTailsShader == null ||
                HeightMapTexture == null ||
                visibleParcelsComputeBuffer == null ||
                visibleparcelCountComputeBuffer == null ||
                grassInstancesComputeBuffer == null ||
                TerrainBlendTexture == null)
                return;

            flower2DrawArgs.SetData(flower2Args);

            // Set up compute shader (refresh constants every dispatch)


            var terrainBounds = new Vector4(terrainGenerator.TerrainModel.MinParcel.x, terrainGenerator.TerrainModel.MaxParcel.x + 1,
                terrainGenerator.TerrainModel.MinParcel.y, terrainGenerator.TerrainModel.MaxParcel.y + 1);

            var HeightTextureSize = new int2(8192, 8192);
            //var OccupancyTextureSize = new int2(512, 512);
            var TerrainBlendTextureSize = new int2(1024, 1024);
            var GroundDetailTextureSize = new int2(2048, 2048);
            var SandDetailTextureSize = new int2(1024, 1024);

            ScatterCatTailsShader.EnableKeyword("THREADS_16");
            ScatterCatTailsShader.SetInt(ShaderProperties.NThreads, flowerInstancesPerParcel);
            ScatterCatTailsShader.SetVector(ShaderProperties.TerrainBounds, terrainBounds);
            ScatterCatTailsShader.SetFloat(ShaderProperties.TerrainHeight, terrainGenerator.MaxHeight);
            ScatterCatTailsShader.SetInt2(ShaderProperties.HeightTextureSize, HeightTextureSize.x, HeightTextureSize.y);
            int OccupancyHeightWidth = terrainGenerator.OccupancyMap != null ? terrainGenerator.OccupancyMapSize : Texture2D.blackTexture.height;
            ScatterCatTailsShader.SetInt2(ShaderProperties.OccupancyTextureSize, OccupancyHeightWidth, OccupancyHeightWidth);
            ScatterCatTailsShader.SetInt2(ShaderProperties.TerrainBlendTextureSize, TerrainBlendTextureSize.x, TerrainBlendTextureSize.y);
            ScatterCatTailsShader.SetInt2(ShaderProperties.GroundDetailTextureSize, GroundDetailTextureSize.x, GroundDetailTextureSize.y);
            ScatterCatTailsShader.SetInt2(ShaderProperties.SandDetailTextureSize, SandDetailTextureSize.x, SandDetailTextureSize.y);
            ScatterCatTailsShader.SetInt(ShaderProperties.ParcelSize, parcelSize);
            ScatterCatTailsShader.SetFloat(ShaderProperties.FDistanceFieldScale, fDistanceFieldScale);
            ScatterCatTailsShader.SetFloat(ShaderProperties.NHeightMapSize, nHeightMapSize);
            ScatterCatTailsShader.SetFloat(ShaderProperties.FSplatMapTiling, fSplatMapTiling);
            ScatterCatTailsShader.SetFloat(ShaderProperties.MinDistOccupancy, terrainGenerator.OccupancyFloor / 255f);
            ScatterCatTailsShader.SetTexture(ShaderKernels.ScatterCatTailsShader, ShaderProperties.HeightMapTexture, HeightMapTexture);
            ScatterCatTailsShader.SetTexture(ShaderKernels.ScatterCatTailsShader, ShaderProperties.TerrainBlendTexture, TerrainBlendTexture);
            ScatterCatTailsShader.SetTexture(ShaderKernels.ScatterCatTailsShader, ShaderProperties.GroundDetailTexture, GroundDetailTexture);
            ScatterCatTailsShader.SetTexture(ShaderKernels.ScatterCatTailsShader, ShaderProperties.SandDetailTexture, SandDetailTexture);

            ScatterCatTailsShader.SetTexture(ShaderKernels.ScatterCatTailsShader, ShaderProperties.OccupancyTexture,
                terrainGenerator.OccupancyMap != null ? terrainGenerator.OccupancyMap : Texture2D.blackTexture);

            ScatterCatTailsShader.SetBuffer(ShaderKernels.ScatterCatTailsShader, ShaderProperties.VisibleParcels, visibleParcelsComputeBuffer);
            ScatterCatTailsShader.SetBuffer(ShaderKernels.ScatterCatTailsShader, ShaderProperties.VisibleParcelCount, visibleparcelCountComputeBuffer);
            ScatterCatTailsShader.SetBuffer(ShaderKernels.ScatterCatTailsShader, ShaderProperties.Flower2Instances, flower2InstancesComputeBuffer);
            ScatterCatTailsShader.SetBuffer(ShaderKernels.ScatterCatTailsShader, ShaderProperties.DrawArgs2, flower2DrawArgs);

            uint threadGroupSizes_X, threadGroupSizes_Y, threadGroupSizes_Z;
            ScatterCatTailsShader.GetKernelThreadGroupSizes(ShaderKernels.ScatterCatTailsShader, out threadGroupSizes_X, out threadGroupSizes_Y, out threadGroupSizes_Z);

            ScatterCatTailsShader.Dispatch(ShaderKernels.ScatterCatTailsShader,
                Mathf.CeilToInt(flowerInstancesPerParcel * maxVisibleParcels / threadGroupSizes_X),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Y),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Z));
        }

        public void RenderGrass(Camera? camera)
        {
            if (grassDrawArgs == null || visibleParcelsComputeBuffer == null || grassInstancesComputeBuffer == null)
                return;

            var renderParams = new RenderParams
            {
                camera = camera,
                layer = 1, // Default
                receiveShadows = true,
                renderingLayerMask = RenderingLayerMask.defaultRenderingLayerMask,
                worldBounds = indirectRenderingBounds,
                shadowCastingMode = ShadowCastingMode.Off,
            };

            renderParams.material = grassMaterial;
            renderParams.matProps = grassMaterialPropertyBlock;
            renderParams.matProps.SetBuffer(ShaderProperties.PerParcelBuffer, visibleParcelsComputeBuffer);
            renderParams.matProps.SetBuffer(ShaderProperties.PerInstanceBuffer, grassInstancesComputeBuffer);
            renderParams.matProps.SetVector(ShaderProperties.ColorMapParams, new Vector4(2.0f, 0.0f, 0.0f, 0.0f));
            renderParams.shadowCastingMode = ShadowCastingMode.Off;

            Graphics.RenderMeshIndirect(renderParams, grassMesh, grassDrawArgs);
        }

        public void RenderFlowers(Camera camera)
        {
            if (flower0DrawArgs == null || flower1DrawArgs == null || flower2DrawArgs == null ||
                flower0InstancesComputeBuffer == null || flower1InstancesComputeBuffer == null || flower2InstancesComputeBuffer == null ||
                visibleParcelsComputeBuffer == null)
                return;

            var renderParams = new RenderParams
            {
                camera = camera,
                layer = 1, // Default
                receiveShadows = true,
                renderingLayerMask = RenderingLayerMask.defaultRenderingLayerMask,
                worldBounds = indirectRenderingBounds,
                shadowCastingMode = ShadowCastingMode.Off,
            };

            renderParams.matProps = flowersMaterialPropertyBlock;
            renderParams.matProps.SetBuffer(ShaderProperties.PerParcelBuffer, visibleParcelsComputeBuffer);
            renderParams.matProps.SetFloat(ShaderProperties.BatchingBlockSize, flowerInstancesPerParcel);
            renderParams.shadowCastingMode = ShadowCastingMode.Off;

            renderParams.material = flower0Material;
            renderParams.matProps.SetBuffer(ShaderProperties.PerInstanceBuffer, flower0InstancesComputeBuffer);
            Graphics.RenderMeshIndirect(renderParams, flower0Mesh, flower0DrawArgs);

            renderParams.material = flower1Material;
            renderParams.matProps.SetBuffer(ShaderProperties.PerInstanceBuffer, flower1InstancesComputeBuffer);
            Graphics.RenderMeshIndirect(renderParams, flower1Mesh, flower1DrawArgs);

            renderParams.material = flower2Material;
            renderParams.matProps.SetBuffer(ShaderProperties.PerInstanceBuffer, flower2InstancesComputeBuffer);
            Graphics.RenderMeshIndirect(renderParams, flower2Mesh, flower2DrawArgs);
        }

        private void OnDestroy()
        {
            ReleaseBuffers();
        }

        private void ReleaseBuffers()
        {
            quadTreeNodesComputeBuffer?.Release();
            visibleParcelsComputeBuffer?.Release();
            visibleparcelCountComputeBuffer?.Release();
            grassInstancesComputeBuffer?.Release();
            flower0InstancesComputeBuffer?.Release();
            flower1InstancesComputeBuffer?.Release();
            flower2InstancesComputeBuffer?.Release();
            grassDrawArgs?.Release();
            flower0DrawArgs?.Release();
            flower1DrawArgs?.Release();
            flower2DrawArgs?.Release();
        }
    }
}
