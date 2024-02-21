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
    public NativeArray<float4> GetSysmemBuffer()
    {
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
    //[BurstCompile]
    [BurstCompile]
    public JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
    {
        if (m_initialized && instanceCount_ > 0)
        {
            uint nBucketCount = 1;
            int alignment = UnsafeUtility.AlignOf<long>();
            BatchCullingOutputDrawCommands* drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            drawCommands -> drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>() * nBucketCount, alignment, Allocator.TempJob);
            drawCommands -> drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(), alignment, Allocator.TempJob);
            drawCommands -> drawCommandCount = (int)nBucketCount; // The number of elements in the BatchCullingOutputDrawCommands.drawCommands array.
            drawCommands -> drawRangeCount = 1;
            drawCommands -> drawCommandPickingInstanceIDs = null;
            drawCommands -> visibleInstances = (int*)UnsafeUtility.Malloc(instanceCount_ * sizeof(int), alignment, Allocator.TempJob);
            drawCommands -> visibleInstanceCount = instanceCount_;
            drawCommands -> instanceSortingPositions = null; // If BatchDrawCommandFlags.HasSortingPosition is set for one or more draw commands, the instanceSortingPositions array contains explicit float3 world space positions that Unity uses for depth sorting.The culling callback must allocate the memory for the instanceSortingPositions using the UnsafeUtility.Malloc method and the Allocator.TempJob parameter. The memory is released by Unity when the rendering is complete.If the length of the array is 0, set its value to null.
            drawCommands -> instanceSortingPositionFloatCount = 0; // If BatchDrawCommandFlags.HasSortingPosition is set for one or more draw commands, this contains float3 world-space positions that Unity uses for depth sorting.

            drawCommands -> drawRanges[0].drawCommandsBegin = 0;
            drawCommands -> drawRanges[0].drawCommandsCount = nBucketCount;
            drawCommands -> drawRanges[0].filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, layer = 0, motionMode = MotionVectorGenerationMode.Camera, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, staticShadowCaster = false, allDepthSorted = false };

            // Set capacities
            NativeList<UInt32> list = new (Allocator.TempJob);
            list.Resize(instanceCount_, NativeArrayOptions.UninitializedMemory);

            NativeArray<int> _bucketRanges = new NativeArray<int>((int)nBucketCount, Allocator.TempJob);

            for (int i = 0; i < nBucketCount; ++i) { _bucketRanges[i] = i * 10; }

            int instanceInverseCount = instanceCount_;

            long* bucketSizes = (long*)UnsafeUtility.Malloc(sizeof(long), alignment, Allocator.TempJob);
            *bucketSizes = 0;
            UnsafeAtomicCounter64 bucketSizesAtomic = new UnsafeAtomicCounter64(bucketSizes);

            NativeArray<long> nBucketBitShift = new NativeArray<long>((int)nBucketCount, Allocator.TempJob);
            for (long i = 0; i < nBucketCount; ++i)
            {
                nBucketBitShift[(int)i] = (i * 65535) + 1;
            }

            // Convert to NativeArray
            NativeArray<int> visibleInstanceArray =
                NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>(drawCommands -> visibleInstances, instanceCount_, Allocator.Invalid);

            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref visibleInstanceArray, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
            #endif

            BatchFrustumCullAndDistanceCalcJob BFCDC_Job = new BatchFrustumCullAndDistanceCalcJob(
                cullingContext,
                m_sysmemBuffer,
                list
            );

            DistanceSortingJob DS_Job = new DistanceSortingJob(list);

            // visible instance must be sorted
            BatchDistanceRemovalAndBucketJob BDRB_Job = new BatchDistanceRemovalAndBucketJob(
                list,
                visibleInstanceArray,
                nBucketCount,
                _bucketRanges,
                instanceInverseCount,
                bucketSizesAtomic,
                nBucketBitShift
            );

            BatchRenderGroupCommitJob BRGC_Job = new BatchRenderGroupCommitJob(
                drawCommands,
                m_batchID,
                m_materialID,
                m_meshID,
                nBucketCount,
                instanceInverseCount,
                bucketSizes
            );

            JobHandle batchCullJobHandle = BFCDC_Job.Schedule(instanceCount_, 1000);
            JobHandle distSortJobHandle = DS_Job.Schedule(batchCullJobHandle);
            JobHandle BatchDistanceRemovalAndBucketJobHandle = BDRB_Job.Schedule(instanceCount_, 10, distSortJobHandle);
            JobHandle BatchRenderGroupCommitJobHandle = BRGC_Job.Schedule(1, (int)1, BatchDistanceRemovalAndBucketJobHandle);
            JobHandle Dispose_List = list.Dispose(BatchRenderGroupCommitJobHandle);
            JobHandle Dispose_BucketRanges = _bucketRanges.Dispose(Dispose_List);
            return Dispose_BucketRanges;
        }

        return default(JobHandle);
    }

    [BurstCompile]
    public struct BatchFrustumCullAndDistanceCalcJob : IJobParallelForBatch
    {
        [ReadOnly]
        public readonly BatchCullingContext cullingContext;

        [ReadOnly]
        public readonly NativeArray<float4> sysmem;

        [WriteOnly]
        public NativeList<UInt32> List;

        public BatchFrustumCullAndDistanceCalcJob(BatchCullingContext cullingContext,
            NativeArray<float4> sysmem,
            NativeList<UInt32> list
        )
        {
            this.cullingContext = cullingContext;
            this.sysmem = sysmem;
            this.List = list;
        }

        public void Execute(int startIndex, int count)
        {
            float radius = 0.5f;
            for (int x = startIndex; x < startIndex + count; ++x)
            {
                int index = (x * 3) + 2;
                Vector3 p = sysmem[index].yzw;
                bool result = true;
                float distance;
                for (int i = 0; i < 6; i++)
                {
                    distance = cullingContext.cullingPlanes[i].GetDistanceToPoint(p);

                    if (distance < -radius)
                    {
                        result = false;
                        break;
                    }
                }

                float fDist = (p - cullingContext.lodParameters.cameraPosition).magnitude;
                UInt32 intDist = (UInt32)fDist;
                UInt32 intDist32 = ((UInt32)intDist) << 16;
                UInt32 newDistIndex = (UInt32)x | intDist32;
                List[x] = newDistIndex;

                // if (result == true)
                // {
                //     float fDist = (p - cullingContext.lodParameters.cameraPosition).magnitude;
                //     UInt32 intDist = (UInt32)fDist;
                //     UInt32 intDist32 = ((UInt32)intDist) << 16;
                //     UInt32 newDistIndex = (UInt32)x | intDist32;
                //     List[x] = newDistIndex;
                // }
                // else
                // {
                //     UInt32 newDistIndex = (UInt32)x | (UInt32)65535 << 16;;
                //     List[x] = newDistIndex;
                // }
            }
        }
    }

    [BurstCompile]
    public struct DistanceSortingJob : IJob
    {
        public NativeList<UInt32> List;

        public DistanceSortingJob(NativeList<UInt32> list)
        {
            this.List = list;
        }

        public void Execute()
        {
            List.Sort();
        }
    }

    [BurstCompile]
    public struct BatchDistanceRemovalAndBucketJob : IJobParallelForBatch
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> _visInstArray;

        [ReadOnly]
        public uint _bucketCount;

        [WriteOnly]
        public int _instanceInverseCount;

        [ReadOnly]
        public NativeArray<int> _bucketRanges;

        [ReadOnly]
        public NativeList<UInt32> List;

        [NativeDisableUnsafePtrRestriction]
        public UnsafeAtomicCounter64 _bucketSizesAtomic;

        [ReadOnly]
        public NativeArray<long> _nBucketBitShift;

        public BatchDistanceRemovalAndBucketJob(NativeList<UInt32> list,
            NativeArray<int> visInstArray,
            uint bucketCount,
            NativeArray<int> bucketRanges,
            int instanceInverseCount,
            UnsafeAtomicCounter64 bucketSizesAtomic,
            NativeArray<long> nBucketBitShift
        )
        {
            _visInstArray = visInstArray;
            this.List = list;
            _bucketCount = bucketCount;
            _bucketRanges = bucketRanges;
            _instanceInverseCount = instanceInverseCount;
            _bucketSizesAtomic = bucketSizesAtomic;
            _nBucketBitShift = nBucketBitShift;
        }

        public void Execute(int startIndex, int count)
        {
            for (int x = startIndex; x < startIndex + count; ++x)
            {
                UInt32 upperBitsMask = (UInt32)65535 << 16;
                UInt32 rawValue = List[x];
                UInt32 nDistMasked = (rawValue & upperBitsMask);
                UInt32 nDist = (nDistMasked) >> 16;

                _bucketSizesAtomic.Add(_nBucketBitShift[0]);
                // for (int nBucketIndex = 0; nBucketIndex < _bucketCount-1; ++nBucketIndex)
                // {
                //     if (nDist < _bucketRanges[nBucketIndex + 1])
                //     {
                //         _bucketSizesAtomic.Add(_nBucketBitShift[nBucketIndex]);
                //         break;
                //     }
                // }

                _visInstArray[x] = (int)(rawValue & 65535);
            }
        }
    }

    [BurstCompile]
    public struct BatchRenderGroupCommitJob : IJobParallelForBatch
    {
        [NativeDisableUnsafePtrRestriction]
        public BatchCullingOutputDrawCommands* _drawCommands;

        private BatchID _batchID;
        private BatchMaterialID _materialID;
        private BatchMeshID _meshID;

        [ReadOnly]
        public uint _bucketCount;

        [ReadOnly]
        public int _instanceCount;

        [NativeDisableUnsafePtrRestriction]
        public long* _bucketSizes;

        public BatchRenderGroupCommitJob(BatchCullingOutputDrawCommands* drawCommands,
            BatchID batchID,
            BatchMaterialID materialID,
            BatchMeshID meshID,
            uint bucketCount,
            int instanceCount,
            long* bucketSizes
        )
        {
            _drawCommands = drawCommands;
            _batchID = batchID;
            _materialID = materialID;
            _meshID = meshID;
            _bucketCount = bucketCount;
            _instanceCount = instanceCount;
            _bucketSizes = bucketSizes;
        }

        public void Execute(int startIndex, int count)
        {
            int nVisibleOffset = 0;
            int nVisibleCount = _instanceCount;

            long bitmask0 = ((long)65535 << 0);
            long bitmask1 = ((long)65535 << 16);
            long bitmask2 = ((long)65535 << 32);
            long bitmask3 = ((long)65535 << 48);
            int bucket0 = (int)(((*_bucketSizes) & bitmask0) >> 0);
            int bucket1 = (int)(((*_bucketSizes) & bitmask1) >> 16);
            int bucket2 = (int)(((*_bucketSizes) & bitmask2) >> 32);
            int bucket3 = (int)(((*_bucketSizes) & bitmask3) >> 48);

            if (startIndex == 0)
            {
                nVisibleOffset = 0;
                nVisibleCount = bucket0;
            }
            else if (startIndex == 1)
            {
                nVisibleOffset = bucket0;
                nVisibleCount = bucket1;
            }
            else if (startIndex == 2)
            {
                nVisibleOffset = bucket0 + bucket1;
                nVisibleCount = bucket2;
            }
            else if (startIndex == 3) // we're in the last bucket
            {
                nVisibleOffset = bucket0 + bucket1 + bucket2;
                nVisibleCount = bucket3;
            }

            _drawCommands -> drawCommands[startIndex].visibleOffset = (uint)nVisibleOffset;
            _drawCommands -> drawCommands[startIndex].visibleCount = (uint)(nVisibleCount);
            _drawCommands -> drawCommands[startIndex].batchID = _batchID;
            _drawCommands -> drawCommands[startIndex].materialID = _materialID;
            _drawCommands -> drawCommands[startIndex].meshID = _meshID;
            _drawCommands -> drawCommands[startIndex].submeshIndex = 0;
            _drawCommands -> drawCommands[startIndex].splitVisibilityMask = 0xff;
            _drawCommands -> drawCommands[startIndex].flags = 0;
            _drawCommands -> drawCommands[startIndex].sortingPosition = 0;
        }
    }
}
