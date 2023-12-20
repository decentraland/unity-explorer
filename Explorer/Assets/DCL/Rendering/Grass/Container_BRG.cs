using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

/*
    This class handle rendering of ground cells & debris using BRG.
    Both ground cells & debris could be rendered using the same GPU data layout:
        - obj2world matrix ( 3 * float4 )
        - world2obj matrix ( 3 * float4 )
        - color ( 1 * float4 )

    so 7 float4 per mesh.

    Do not forget data is stored in SoA

*/


public unsafe class BRG_Container
{
    // In GLES mode, BRG raw buffer is a constant buffer (UBO)
    private bool UseConstantBuffer => BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer;
    private bool m_castShadows;

    private int m_maxInstances; // maximum item in this container
    private int m_instanceCount; // current item count
    private int m_alignedGPUWindowSize; // BRG raw window size
    private int m_maxInstancePerWindow; // max instance per window (
    //private int m_windowCount; // amount of window (1 in SSBO mode, n in UBO mode)
    private int m_totalGpuBufferSize; // total size of the raw buffer
    private NativeArray<float4> m_sysmemBuffer; // system memory copy of the raw buffer
    private bool m_initialized;
    private int m_instanceSize; // item size, in bytes
    //private BatchID[] m_batchIDs; // one batchID per window
    private BatchID m_batchID;
    private BatchMaterialID m_materialID;
    private BatchMeshID m_meshID;
    private BatchRendererGroup m_BatchRendererGroup; // BRG object
    private GraphicsBuffer m_GPUPersistentInstanceData; // GPU raw buffer (could be SSBO or UBO)

    private static int instanceCount = 1000 * 1000; // 1,000 by 1,000 blades of grass as a grid

    // matrices
    private static int objWorld_Offset = 0;
    private static int objWorld_TypeSize = 3 * 4 * 4; // Matrix 3x4 - 4Byte(32bit) each
    private static int objWorld_InstanceCount = instanceCount; // 1,000 by 1,000 blades of grass as a grid
    private static int objWorld_Size = objWorld_TypeSize * objWorld_InstanceCount;

    // inverse matrices
    private static int worldObj_Offset = objWorld_Size;
    private static int worldObj_TypeSize = 3 * 4 * 4; // Matrix 3x4 - 4Byte(32bit) each
    private static int worldObj_InstanceCount = instanceCount; // 1,000 by 1,000 blades of grass as a grid
    private static int worldObj_Size = worldObj_TypeSize * worldObj_InstanceCount;

    // colors
    private static int colour_Offset = objWorld_Size + worldObj_Size;
    private static int colour_TypeSize = 4; // Colour - 4Byte(32bit) each channel 8bit
    private static int colour_InstanceCount = instanceCount; // 1,000 by 1,000 blades of grass as a grid
    private static int colour_Size = colour_TypeSize * colour_InstanceCount;

    private static int rawBufferSize = objWorld_Size + worldObj_Size + colour_Size;

    // Create a BRG object and allocate buffers.
    public bool Init(Mesh mesh, Material mat, int maxInstances, int instanceSize, bool castShadows)
    {
        // Create the BRG object, specifying our BRG callback
        m_BatchRendererGroup = new BatchRendererGroup(this.OnPerformCulling, IntPtr.Zero);
        m_GPUPersistentInstanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw, count: rawBufferSize / 4, 4);

        // In our sample game we're dealing with 3 instanced properties: obj2world, world2obj and baseColor
        var batchMetadata = new NativeArray<MetadataValue>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        // Batch metadata buffer
        int objectToWorldID = Shader.PropertyToID("unity_ObjectToWorld");
        int worldToObjectID = Shader.PropertyToID("unity_WorldToObject");
        int colorID = Shader.PropertyToID("_BaseColor");

        // Create system memory copy of big GPU raw buffer
        m_sysmemBuffer = new NativeArray<float4>(instanceCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        m_batchID = new BatchID();
        batchMetadata[0] = CreateMetadataValue(objectToWorldID, gpuOffset: objWorld_Offset, true);  // matrices
        batchMetadata[1] = CreateMetadataValue(worldToObjectID, gpuOffset: worldObj_Offset, true);  // inverse matrices
        batchMetadata[2] = CreateMetadataValue(colorID, gpuOffset: colour_Offset, true);            // colors
        m_batchID = m_BatchRendererGroup.AddBatch(batchMetadata, m_GPUPersistentInstanceData.bufferHandle, bufferOffset: 0, windowSize: 0);

        // we don't need this metadata description array anymore
        batchMetadata.Dispose();

        // Setup very large bound to be sure BRG is never culled
        UnityEngine.Bounds bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1048576.0f, 1048576.0f, 1048576.0f));
        m_BatchRendererGroup.SetGlobalBounds(bounds);

        // Register mesh
        if (mesh)
            m_meshID = m_BatchRendererGroup.RegisterMesh(mesh);

        // Register material
        if (mat)
            m_materialID = m_BatchRendererGroup.RegisterMaterial(mat);

        m_initialized = true;
        return true;
    }

    //  Upload minimal GPU data according to "instanceCount"
    //  Because of SoA and this class is managing 3 BRG properties ( 2 matrices & 1 color ), the last window could use up to 3 SetData
    [BurstCompile]
    public bool UploadGpuData(int instanceCount)
    {
        m_GPUPersistentInstanceData.SetData(m_sysmemBuffer, 0, 0, 1000);
        return true;
    }

    // Release all allocated buffers
    public void Shutdown()
    {
        if (m_initialized)
        {
            m_BatchRendererGroup.RemoveBatch(m_batchID);
            m_BatchRendererGroup.UnregisterMaterial(m_materialID);
            m_BatchRendererGroup.UnregisterMesh(m_meshID);
            m_BatchRendererGroup.Dispose();
            m_GPUPersistentInstanceData.Dispose();
            m_sysmemBuffer.Dispose();
        }
    }

    // return the system memory buffer and the window size, so BRG_Background and BRG_Debris can fill the buffer with new content
    public NativeArray<float4> GetSysmemBuffer(out int totalSize, out int alignedWindowSize)
    {
        totalSize = m_totalGpuBufferSize;
        alignedWindowSize = m_alignedGPUWindowSize;
        return m_sysmemBuffer;
    }

    // helper function to create the 32bits metadata value. Bit 31 means property have different value per instance
    static MetadataValue CreateMetadataValue(int nameID, int gpuOffset, bool isPerInstance)
    {
        const uint kIsPerInstanceBit = 0x80000000; // Last bit in a 32bit value
        return new MetadataValue
        {
            NameID = nameID,
            Value = (uint)gpuOffset | (isPerInstance ? (kIsPerInstanceBit) : 0),
        };
    }

    // Helper function to allocate BRG buffers during the BRG callback function
    private static T* Malloc<T>(uint count) where T : unmanaged
    {
        return (T*)UnsafeUtility.Malloc(
            UnsafeUtility.SizeOf<T>() * count,
            UnsafeUtility.AlignOf<T>(),
            Allocator.TempJob);
    }

    // Main BRG entry point per frame. In this sample we won't use BatchCullingContext as we don't need culling
    // This callback is responsible to fill cullingOutput with all draw commands we need to render all the items
    [BurstCompile]
    public JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
    {
        if (m_initialized)
        {
            BatchCullingOutputDrawCommands drawCommands = new BatchCullingOutputDrawCommands();

            drawCommands.drawCommandCount = 1;

            // Allocate a single BatchDrawRange. ( all our draw commands will refer to this BatchDrawRange)
            drawCommands.drawRangeCount = 1;
            drawCommands.drawRanges = Malloc<BatchDrawRange>(1);
            drawCommands.drawRanges[0] = new BatchDrawRange
            {
                drawCommandsBegin = 0,
                drawCommandsCount = (uint)1,
                filterSettings = new BatchFilterSettings
                {
                    renderingLayerMask = 1,
                    layer = 0,
                    motionMode = MotionVectorGenerationMode.Camera,
                    shadowCastingMode = m_castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    staticShadowCaster = false,
                    allDepthSorted = false
                }
            };

            if (drawCommands.drawCommandCount > 0)
            {
                // as we don't need culling, the visibility int array buffer will always be {0,1,2,3,...} for each draw command
                // so we just allocate maxInstancePerDrawCommand and fill it
                drawCommands.visibleInstances = Malloc<int>((uint)instanceCount);

                // As we don't need any frustum culling in our context, we fill the visibility array with {0,1,2,3,...}
                for (int i = 0; i < instanceCount; i++)
                    drawCommands.visibleInstances[i] = i;

                // Allocate the BatchDrawCommand array (drawCommandCount entries)
                // In SSBO mode, drawCommandCount will be just 1
                drawCommands.drawCommands = Malloc<BatchDrawCommand>((uint)instanceCount);
                int inBatchCount = instanceCount;//left > maxInstancePerDrawCommand ? maxInstancePerDrawCommand : left;
                drawCommands.drawCommands[0] = new BatchDrawCommand
                {
                    visibleOffset = (uint)0,    // all draw command is using the same {0,1,2,3...} visibility int array
                    visibleCount = (uint)inBatchCount,
                    //batchID = m_batchIDs[b],
                    batchID = m_batchID,
                    materialID = m_materialID,
                    meshID = m_meshID,
                    submeshIndex = 0,
                    splitVisibilityMask = 0xff,
                    flags = BatchDrawCommandFlags.None,
                    sortingPosition = 0
                };
            }

            cullingOutput.drawCommands[0] = drawCommands;
            drawCommands.instanceSortingPositions = null;
            drawCommands.instanceSortingPositionFloatCount = 0;
        }

        return new JobHandle();
    }
}
