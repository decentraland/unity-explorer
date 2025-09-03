using DCL.Landscape;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
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

        public static uint CreateDepth8CornerIndexStart(byte depth, uint cornerIndexStart) =>
            ((uint)depth << 24) | cornerIndexStart;

        public void Initialize(TerrainGenerator terrainGenerator)
        {
            if (initialized)
                return;

            initialized = true;

            indirectRenderingBounds.SetMinMax(
                new Vector3(terrainGenerator.TerrainModel.MinInUnits.x, 0f, terrainGenerator.TerrainModel.MinInUnits.y),
                new Vector3(terrainGenerator.TerrainModel.MaxInUnits.x, TerrainGenerator.MAX_HEIGHT, terrainGenerator.TerrainModel.MaxInUnits.y));

            GenerateQuadTree();
            SetupComputeBuffers();
        }

        public void Render(LandscapeData landscapeData, TerrainGenerator terrainGenerator,
            Camera camera, bool renderToAllCameras)
        {
            Initialize(terrainGenerator);

            RunFrustumCulling(landscapeData, terrainGenerator, camera);
            GenerateScatteredGrass(terrainGenerator);
            arrInstCount.SetData(arrLOD, 0, 0, 3);
            GenerateScatteredFlowers(terrainGenerator);
            GenerateScatteredCatTails(terrainGenerator);

            SetGlobalWindVector();

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
            visibleParcelsComputeBuffer = new ComputeBuffer(512 * 512, sizeof(int) * 2);
            visibleparcelCountComputeBuffer = new ComputeBuffer(1, sizeof(int));
            grassInstancesComputeBuffer = new ComputeBuffer(256 * 256, Marshal.SizeOf<PerInst>());
            flower0InstancesComputeBuffer = new ComputeBuffer(64 * 256, Marshal.SizeOf<PerInst>());
            flower1InstancesComputeBuffer = new ComputeBuffer(64 * 256, Marshal.SizeOf<PerInst>());
            flower2InstancesComputeBuffer = new ComputeBuffer(64 * 256, Marshal.SizeOf<PerInst>());
            arrInstCount = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, 3, sizeof(uint));
            grassDrawArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, count: 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            flower0DrawArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, count: 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            flower1DrawArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, count: 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            flower2DrawArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, count: 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);

            quadTreeNodesComputeBuffer.SetData(quadTreeNodes.ToArray());
        }

        public void RunFrustumCulling(LandscapeData landscapeData, TerrainGenerator terrainGenerator,
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
            int kernelIndex = QuadTreeCullingShader.FindKernel("HierarchicalQuadTreeCulling");

            // Set camera data
            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;

            var projMatrix = Matrix4x4.Perspective(camera.fieldOfView, camera.aspect,
                camera.nearClipPlane, Mathf.Clamp(Mathf.Min(camera.farClipPlane, landscapeData.DetailDistance), 0.0f, 200.0f));

            Matrix4x4 viewProjMatrix = projMatrix * viewMatrix;

            var terrainBounds = new Vector4(terrainGenerator.TerrainModel.MinParcel.x, terrainGenerator.TerrainModel.MaxParcel.x + 1,
                terrainGenerator.TerrainModel.MinParcel.y, terrainGenerator.TerrainModel.MaxParcel.y + 1);

            QuadTreeCullingShader.SetMatrix("viewProjMatrix", viewProjMatrix);
            QuadTreeCullingShader.SetVector("TerrainBounds", terrainBounds);

            QuadTreeCullingShader.SetTexture(kernelIndex, "OccupancyTexture",
                terrainGenerator.OccupancyMap != null ? terrainGenerator.OccupancyMap : Texture2D.blackTexture);

            QuadTreeCullingShader.SetBuffer(kernelIndex, "quadTreeNodes", quadTreeNodesComputeBuffer);
            QuadTreeCullingShader.SetBuffer(kernelIndex, "visibleParcels", visibleParcelsComputeBuffer);
            QuadTreeCullingShader.SetBuffer(kernelIndex, "visibleParcelCount", visibleparcelCountComputeBuffer);

            int threadGroups = Mathf.CeilToInt((quadTreeNodes.Length - 87381) / 256.0f);
            QuadTreeCullingShader.Dispatch(kernelIndex, threadGroups, 1, 1);
        }

        public void GenerateScatteredGrass(TerrainGenerator terrainGenerator)
        {
            if (ScatterGrassShader == null ||
                HeightMapTexture == null ||
                visibleParcelsComputeBuffer == null ||
                visibleparcelCountComputeBuffer == null ||
                grassInstancesComputeBuffer == null ||
                TerrainBlendTexture == null)
                return;

            grassDrawArgs.SetData(grassArgs);

            // Set up compute shader
            int kernelIndex = ScatterGrassShader.FindKernel("ScatterGrass");

            var terrainBounds = new Vector4(terrainGenerator.TerrainModel.MinParcel.x, terrainGenerator.TerrainModel.MaxParcel.x + 1,
                terrainGenerator.TerrainModel.MinParcel.y, terrainGenerator.TerrainModel.MaxParcel.y + 1);

            ScatterGrassShader.SetVector("TerrainBounds", terrainBounds);
            ScatterGrassShader.SetFloat("TerrainHeight", TerrainGenerator.MAX_HEIGHT);
            ScatterGrassShader.SetTexture(kernelIndex, "HeightMapTexture", HeightMapTexture);
            ScatterGrassShader.SetTexture(kernelIndex, "TerrainBlendTexture", TerrainBlendTexture);
            ScatterGrassShader.SetTexture(kernelIndex, "GroundDetailTexture", GroundDetailTexture);
            ScatterGrassShader.SetTexture(kernelIndex, "SandDetailTexture", SandDetailTexture);

            ScatterGrassShader.SetTexture(kernelIndex, "OccupancyTexture",
                terrainGenerator.OccupancyMap != null ? terrainGenerator.OccupancyMap : Texture2D.blackTexture);

            ScatterGrassShader.SetBuffer(kernelIndex, "visibleParcels", visibleParcelsComputeBuffer);
            ScatterGrassShader.SetBuffer(kernelIndex, "visibleParcelCount", visibleparcelCountComputeBuffer);
            ScatterGrassShader.SetBuffer(kernelIndex, "grassInstances", grassInstancesComputeBuffer);
            ScatterGrassShader.SetBuffer(kernelIndex, "drawArgs", grassDrawArgs);

            uint threadGroupSizes_X, threadGroupSizes_Y, threadGroupSizes_Z;

            ScatterGrassShader.GetKernelThreadGroupSizes(kernelIndex, out threadGroupSizes_X, out threadGroupSizes_Y,
                out threadGroupSizes_Z);

            ScatterGrassShader.Dispatch(kernelIndex,
                Mathf.CeilToInt(65536.0f / threadGroupSizes_X),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Y),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Z));
        }

        public void GenerateScatteredFlowers(TerrainGenerator terrainGenerator)
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

            // Set up compute shader
            int kernelIndex = ScatterFlowersShader.FindKernel("FlowerScatter");

            var terrainBounds = new Vector4(terrainGenerator.TerrainModel.MinParcel.x, terrainGenerator.TerrainModel.MaxParcel.x + 1,
                terrainGenerator.TerrainModel.MinParcel.y, terrainGenerator.TerrainModel.MaxParcel.y + 1);

            var HeightTextureSize = new int2(8192, 8192);
            var OccupancyTextureSize = new int2(512, 512);
            var TerrainBlendTextureSize = new int2(1024, 1024);
            var GroundDetailTextureSize = new int2(2048, 2048);
            var SandDetailTextureSize = new int2(1024, 1024);

            ScatterFlowersShader.SetVector("TerrainBounds", terrainBounds);
            ScatterFlowersShader.SetFloat("TerrainHeight", 4.0f);
            ScatterFlowersShader.SetInts("HeightTextureSize", HeightTextureSize[0]);
            ScatterFlowersShader.SetInts("OccupancyTextureSize", OccupancyTextureSize[0]);
            ScatterFlowersShader.SetInts("TerrainBlendTextureSize", TerrainBlendTextureSize[0]);
            ScatterFlowersShader.SetInts("GroundDetailTextureSize", GroundDetailTextureSize[0]);
            ScatterFlowersShader.SetInts("SandDetailTextureSize", SandDetailTextureSize[0]);
            ScatterFlowersShader.SetInt("parcelSize", 16);
            ScatterFlowersShader.SetTexture(kernelIndex, "HeightMapTexture", HeightMapTexture);
            ScatterFlowersShader.SetTexture(kernelIndex, "TerrainBlendTexture", TerrainBlendTexture);
            ScatterFlowersShader.SetTexture(kernelIndex, "GroundDetailTexture", GroundDetailTexture);
            ScatterFlowersShader.SetTexture(kernelIndex, "SandDetailTexture", SandDetailTexture);

            ScatterFlowersShader.SetTexture(kernelIndex, "OccupancyTexture",
                terrainGenerator.OccupancyMap != null ? terrainGenerator.OccupancyMap : Texture2D.blackTexture);

            ScatterFlowersShader.SetBuffer(kernelIndex, "visibleParcels", visibleParcelsComputeBuffer);
            ScatterFlowersShader.SetBuffer(kernelIndex, "visibleParcelCount", visibleparcelCountComputeBuffer);
            ScatterFlowersShader.SetBuffer(kernelIndex, "flower0Instances", flower0InstancesComputeBuffer);
            ScatterFlowersShader.SetBuffer(kernelIndex, "flower1Instances", flower1InstancesComputeBuffer);
            ScatterFlowersShader.SetBuffer(kernelIndex, "drawArgs0", flower0DrawArgs);
            ScatterFlowersShader.SetBuffer(kernelIndex, "drawArgs1", flower1DrawArgs);

            uint threadGroupSizes_X, threadGroupSizes_Y, threadGroupSizes_Z;
            ScatterFlowersShader.GetKernelThreadGroupSizes(kernelIndex, out threadGroupSizes_X, out threadGroupSizes_Y, out threadGroupSizes_Z);

            ScatterFlowersShader.Dispatch(kernelIndex,
                Mathf.CeilToInt(16384.0f / threadGroupSizes_X),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Y),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Z));
        }

        public void GenerateScatteredCatTails(TerrainGenerator terrainGenerator)
        {
            if (ScatterCatTailsShader == null ||
                HeightMapTexture == null ||
                visibleParcelsComputeBuffer == null ||
                visibleparcelCountComputeBuffer == null ||
                grassInstancesComputeBuffer == null ||
                TerrainBlendTexture == null)
                return;

            flower2DrawArgs.SetData(flower2Args);

            // Set up compute shader
            int kernelIndex = ScatterCatTailsShader.FindKernel("CatTailScatter");

            var terrainBounds = new Vector4(terrainGenerator.TerrainModel.MinParcel.x, terrainGenerator.TerrainModel.MaxParcel.x + 1,
                terrainGenerator.TerrainModel.MinParcel.y, terrainGenerator.TerrainModel.MaxParcel.y + 1);

            var HeightTextureSize = new int2(8192, 8192);
            var OccupancyTextureSize = new int2(512, 512);
            var TerrainBlendTextureSize = new int2(1024, 1024);
            var GroundDetailTextureSize = new int2(2048, 2048);
            var SandDetailTextureSize = new int2(1024, 1024);

            ScatterCatTailsShader.SetVector("TerrainBounds", terrainBounds);
            ScatterCatTailsShader.SetFloat("TerrainHeight", 4.0f);
            ScatterCatTailsShader.SetInts("HeightTextureSize", HeightTextureSize[0]);
            ScatterCatTailsShader.SetInts("OccupancyTextureSize", OccupancyTextureSize[0]);
            ScatterCatTailsShader.SetInts("TerrainBlendTextureSize", TerrainBlendTextureSize[0]);
            ScatterCatTailsShader.SetInts("GroundDetailTextureSize", GroundDetailTextureSize[0]);
            ScatterCatTailsShader.SetInts("SandDetailTextureSize", SandDetailTextureSize[0]);
            ScatterCatTailsShader.SetInt("parcelSize", 16);
            ScatterCatTailsShader.SetTexture(kernelIndex, "HeightMapTexture", HeightMapTexture);
            ScatterCatTailsShader.SetTexture(kernelIndex, "TerrainBlendTexture", TerrainBlendTexture);
            ScatterCatTailsShader.SetTexture(kernelIndex, "GroundDetailTexture", GroundDetailTexture);
            ScatterCatTailsShader.SetTexture(kernelIndex, "SandDetailTexture", SandDetailTexture);

            ScatterCatTailsShader.SetTexture(kernelIndex, "OccupancyTexture",
                terrainGenerator.OccupancyMap != null ? terrainGenerator.OccupancyMap : Texture2D.blackTexture);

            ScatterCatTailsShader.SetBuffer(kernelIndex, "visibleParcels", visibleParcelsComputeBuffer);
            ScatterCatTailsShader.SetBuffer(kernelIndex, "visibleParcelCount", visibleparcelCountComputeBuffer);
            ScatterCatTailsShader.SetBuffer(kernelIndex, "flower2Instances", flower2InstancesComputeBuffer);
            ScatterCatTailsShader.SetBuffer(kernelIndex, "drawArgs2", flower2DrawArgs);

            uint threadGroupSizes_X, threadGroupSizes_Y, threadGroupSizes_Z;
            ScatterCatTailsShader.GetKernelThreadGroupSizes(kernelIndex, out threadGroupSizes_X, out threadGroupSizes_Y, out threadGroupSizes_Z);

            ScatterCatTailsShader.Dispatch(kernelIndex,
                Mathf.CeilToInt(16384.0f / threadGroupSizes_X),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Y),
                Mathf.CeilToInt(1.0f / threadGroupSizes_Z));
        }

        public static bool SetGlobalWindVector()
        {
            WindZone[] sceneWindZones = FindObjectsByType<WindZone>(FindObjectsSortMode.None);

            for (var i = 0; i < sceneWindZones.Length; i++)
            {
                if (sceneWindZones[i].mode == WindZoneMode.Directional)
                {
                    Shader.SetGlobalVector("_Wind", new Vector4(sceneWindZones[i].windTurbulence, sceneWindZones[i].windPulseMagnitude, sceneWindZones[i].windPulseFrequency, sceneWindZones[i].windMain));
                    return true;
                }
            }

            return false;
        }

        public void RenderGrass(Camera camera)
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
            renderParams.matProps = new MaterialPropertyBlock();
            renderParams.matProps.SetBuffer("_PerParcelBuffer", visibleParcelsComputeBuffer);
            renderParams.matProps.SetBuffer("_PerInstanceBuffer", grassInstancesComputeBuffer);
            renderParams.matProps.SetVector("_ColorMapParams", new Vector4(2.0f, 0.0f, 0.0f, 0.0f));
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

            renderParams.matProps = new MaterialPropertyBlock();
            renderParams.matProps.SetBuffer("_PerParcelBuffer", visibleParcelsComputeBuffer);
            renderParams.matProps.SetFloat("_batchingBlockSize", 64);
            renderParams.shadowCastingMode = ShadowCastingMode.Off;

            renderParams.material = flower0Material;
            renderParams.matProps.SetBuffer("_PerInstanceBuffer", flower0InstancesComputeBuffer);
            Graphics.RenderMeshIndirect(renderParams, flower0Mesh, flower0DrawArgs);

            renderParams.material = flower1Material;
            renderParams.matProps.SetBuffer("_PerInstanceBuffer", flower1InstancesComputeBuffer);
            Graphics.RenderMeshIndirect(renderParams, flower1Mesh, flower1DrawArgs);

            renderParams.material = flower2Material;
            renderParams.matProps.SetBuffer("_PerInstanceBuffer", flower2InstancesComputeBuffer);
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
