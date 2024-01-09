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
    //private bool UseConstantBuffer => BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer;
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


    private int instanceCount_;
    private const int objWorld_TypeSize = 3 * 4 * 4; // Matrix 3x4 - 4Byte(32bit) each
    private const int worldObj_TypeSize = 3 * 4 * 4; // inverse matrices - Matrix 3x4 - 4Byte(32bit) each
    private const int colour_TypeSize = 4 * 4; // Colour - 4Byte(32bit each channel)
    private const int kGpuItemSize = objWorld_TypeSize + worldObj_TypeSize + colour_TypeSize;  //  GPU item size ( 2 * 4x3 matrices plus 1 color per item )

    // Create a BRG object and allocate buffers.
    public bool Init(Mesh mesh, Material mat, int maxInstances, int instanceSize, bool castShadows)
    {
        instanceCount_ = maxInstances;
        int objWorld_Offset = 0;
        int objWorld_Size = objWorld_TypeSize * maxInstances;

        int worldObj_Offset = objWorld_Size;
        int worldObj_Size = worldObj_TypeSize * maxInstances;

        int colour_Offset = objWorld_Size + worldObj_Size;
        int colour_Size = colour_TypeSize * maxInstances;

        int rawBufferSize = objWorld_Size + worldObj_Size + colour_Size;

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
        m_sysmemBuffer = new NativeArray<float4>(maxInstances * 7, Allocator.Persistent, NativeArrayOptions.ClearMemory);

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
        m_GPUPersistentInstanceData.SetData(m_sysmemBuffer, 0, 0, m_sysmemBuffer.Length);
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
        return (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), Allocator.TempJob);
    }

    // Main BRG entry point per frame. In this sample we won't use BatchCullingContext as we don't need culling
    // This callback is responsible to fill cullingOutput with all draw commands we need to render all the items
    [BurstCompile]
    public JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
    {
        if (m_initialized)
        {
            int alignment = UnsafeUtility.AlignOf<long>();
            BatchCullingOutputDrawCommands* drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            drawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>(), alignment, Allocator.TempJob);
            drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(), alignment, Allocator.TempJob);
            drawCommands->drawCommandPickingInstanceIDs = null;

            drawCommands->drawCommandCount = 1; // The number of elements in the BatchCullingOutputDrawCommands.drawCommands array.
            drawCommands->drawRangeCount = 1;

            // Configure the single draw command to draw kNumInstances instances
            // starting from offset 0 in the array, using the batch, material and mesh
            // IDs registered in the Start() method. It doesn't set any special flags.
            drawCommands->drawCommands[0].visibleOffset = 0;
            drawCommands->drawCommands[0].visibleCount = (uint)instanceCount_;
            drawCommands->drawCommands[0].batchID = m_batchID;
            drawCommands->drawCommands[0].materialID = m_materialID;
            drawCommands->drawCommands[0].meshID = m_meshID;
            drawCommands->drawCommands[0].submeshIndex = 0;
            drawCommands->drawCommands[0].splitVisibilityMask = 0xff;
            drawCommands->drawCommands[0].flags = 0;
            drawCommands->drawCommands[0].sortingPosition = 0;

            drawCommands->drawRanges[0].drawCommandsBegin = 0;
            drawCommands->drawRanges[0].drawCommandsCount = 1;
            drawCommands->drawRanges[0].filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, layer = 0, motionMode = MotionVectorGenerationMode.Camera, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, staticShadowCaster = false, allDepthSorted = false };

            drawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(instanceCount_ * sizeof(int), alignment, Allocator.TempJob);
            drawCommands->visibleInstanceCount = instanceCount_;
            for (int i = 0; i < drawCommands->visibleInstanceCount; ++i)
                drawCommands->visibleInstances[i] = i;

            drawCommands->instanceSortingPositions = null; // If BatchDrawCommandFlags.HasSortingPosition is set for one or more draw commands, the instanceSortingPositions array contains explicit float3 world space positions that Unity uses for depth sorting.The culling callback must allocate the memory for the instanceSortingPositions using the UnsafeUtility.Malloc method and the Allocator.TempJob parameter. The memory is released by Unity when the rendering is complete.If the length of the array is 0, set its value to null.
            drawCommands->instanceSortingPositionFloatCount = 0; // If BatchDrawCommandFlags.HasSortingPosition is set for one or more draw commands, this contains float3 world-space positions that Unity uses for depth sorting.
        }

        return new JobHandle();
    }
}
