using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Profiling;
using System.Collections.Generic;
using UnityEngine.Serialization;

public unsafe class LandscapeBatch : MonoBehaviour
{
    public Mesh m_mesh;
    public Material m_material;
    public bool m_castShadows;

    static readonly ProfilerMarker s_GrassGPUSetDataProfileMarker = new ProfilerMarker("BRG_Background.GPUSetData");

    private const int kGpuItemSize = (3 * 2 + 1) * 16;  //  GPU item size ( 2 * 4x3 matrices plus 1 color per item )

    private BRG_Container m_brgContainer;
    private JobHandle m_updateJobFence;

    private const int nGridDimension = 100;
    private const int instanceCount = nGridDimension * nGridDimension; // 1,000 by 1,000 blades of grass as a grid

    private const int objWorld_TypeSize = 3 * 4 * 4; // Matrix 3x4 - 4Byte(32bit) each
    private const int worldObj_TypeSize = 3 * 4 * 4; // inverse matrices - Matrix 3x4 - 4Byte(32bit) each
    private const int colour_TypeSize = 4 * 4; // Colour - 4Byte(32bit each channel)

    private static bool bIsCPUBufferCreated = false;
    private static bool bIsCPUBufferTransferredToGPU = false;

    public void Awake()
    {

    }

    // Start is called before the first frame update
    void Start()
    {
        m_brgContainer = new BRG_Container();
        m_brgContainer.Init(m_mesh, m_material, instanceCount, kGpuItemSize, m_castShadows);
        m_brgContainer.UploadGpuData(instanceCount);
    }

    [BurstCompile]
    private struct UpdateCPUBufferJob : IJobFor
    {
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float4> _sysmemBuffer;

        public void Execute(int rowIndex)
        {
            int itemId = rowIndex * nGridDimension;

            float fScale = 0.2f;
            float z = rowIndex * fScale;
            for (int x = 0; x < nGridDimension; ++x)
            {
                int offset = (itemId * 3) + (x * 3);
                // compute the new current frame matrix
                _sysmemBuffer[offset + 0] = new float4(1, 0, 0, 0);
                _sysmemBuffer[offset + 1] = new float4(1, 0, 0, 0);
                _sysmemBuffer[offset + 2] = new float4(1, x * fScale, 0, z);
            }

            int offset_inverse_matrix = (itemId * 3) + (3 * instanceCount);
            for (int x = 0; x < nGridDimension; ++x)
            {
                int offset = offset_inverse_matrix + (x * 3);
                // compute the new inverse matrix (note: shortcut use identity because aligned cubes normals aren't affected by any non uniform scale
                _sysmemBuffer[offset + 0] = new float4(1, 1, 1, 1);
                _sysmemBuffer[offset + 1] = new float4(1, 1, 1, 1);
                _sysmemBuffer[offset + 2] = new float4(1, 1, 1, 1);
            }

            int offset_colour = itemId + (3 * 2 * instanceCount);
            for (int x = 0; x < nGridDimension; ++x)
            {
                // update colors
                _sysmemBuffer[offset_colour + x] = new float4(1, 1, 1, 0);
            }
        }
    }

    [BurstCompile]
    JobHandle UpdateCPUBuffer(JobHandle jobFence)
    {
        NativeArray<float4> sysmemBuffer = m_brgContainer.GetSysmemBuffer();

        UpdateCPUBufferJob myJob = new UpdateCPUBufferJob()
        {
            _sysmemBuffer = sysmemBuffer,
        };
        jobFence = myJob.ScheduleParallel(nGridDimension, 4, jobFence);      // 4 slices per job
        return jobFence;
    }

    // Update is called once per frame
    void Update()
    {
        if (bIsCPUBufferCreated == false)
        {
            JobHandle jobFence = new JobHandle();
            m_updateJobFence = UpdateCPUBuffer(jobFence);
            bIsCPUBufferCreated = true;
        }
    }

    private void LateUpdate()
    {
        m_updateJobFence.Complete();

        if (bIsCPUBufferTransferredToGPU == false)
        {
            s_GrassGPUSetDataProfileMarker.Begin();
            m_brgContainer.UploadGpuData(instanceCount);
            s_GrassGPUSetDataProfileMarker.End();
            bIsCPUBufferTransferredToGPU = true;
        }
    }

    private void OnDestroy()
    {
        if ( m_brgContainer != null )
            m_brgContainer.Shutdown();
    }
}
