using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Profiling;
using System.Collections.Generic;
using UnityEngine.Serialization;

public unsafe class Grass_BRG : MonoBehaviour
{
    public static Grass_BRG gGrassManager;

    public Mesh m_mesh;
    public Material m_material;
    public bool m_castShadows;
    public float m_motionSpeed = 3.0f;

    static readonly ProfilerMarker s_GrassGPUSetDataProfileMarker = new ProfilerMarker("BRG_Background.GPUSetData");
    //static readonly ProfilerMarker s_DebrisGPUSetData = new ProfilerMarker("BRG_Debris.GPUSetData");

    private const int kGpuItemSize = (3 * 2 + 1) * 16;  //  GPU item size ( 2 * 4x3 matrices plus 1 color per item )

    private BRG_Container m_brgContainer;
    private JobHandle m_updateJobFence;

    private const int nGridDimension = 100;
    private const int instanceCount = nGridDimension * nGridDimension; // 1,000 by 1,000 blades of grass as a grid
    private float m_phase = 0.0f;

    private uint m_slicePos;

    private const int objWorld_TypeSize = 3 * 4 * 4; // Matrix 3x4 - 4Byte(32bit) each
    private const int worldObj_TypeSize = 3 * 4 * 4; // inverse matrices - Matrix 3x4 - 4Byte(32bit) each
    private const int colour_TypeSize = 4 * 4; // Colour - 4Byte(32bit each channel)

    private static bool bIsCPUBufferCreated = false;
    private static bool bIsCPUBufferTransferredToGPU = false;

    // Data:
    // Position (float3)
    // Facing dir (float2)
    // Wind strength globally
    // Wind strength locally i.e. at position (moving 2D perlin noise texture - sampled via CPU & GPU)
    // Per-blade hash
    // Grass Type
    // Clump facing (float2)
    // Clump colour
    // Height
    // Width
    // Tilt
    // Bend
    // Side Curve

    public struct GrassData
    {
        public float width;
        public float height;
        public Vector4 baseColour;
        public Vector4 tipColour;
    };

    [FormerlySerializedAs("m_backgroundItems")] public NativeArray<GrassData> m_GrassDataArray;

    public void Awake()
    {
        gGrassManager = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        m_brgContainer = new BRG_Container();
        m_brgContainer.Init(CreateGrassBladeMesh(), m_material, instanceCount, kGpuItemSize, m_castShadows);

        // setup positions & scale of each background elements
        //m_GrassDataArray = new NativeArray<GrassData>(instanceCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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
    JobHandle UpdateCPUBuffer(float smoothScroll, float dt, JobHandle jobFence)
    {
        int totalGpuBufferSize;
        int alignedWindowSize;
        NativeArray<float4> sysmemBuffer = m_brgContainer.GetSysmemBuffer(out totalGpuBufferSize, out alignedWindowSize);

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
        float dt = Time.deltaTime;
        m_phase += dt * m_motionSpeed;
        //m_burstTimer -= dt;

        // while (m_phase >= 1.0f)
        // {
        //     m_phase -= 1.0f;
        // }


        if (bIsCPUBufferCreated == false)
        {
            JobHandle jobFence = new JobHandle();
            m_updateJobFence = UpdateCPUBuffer(m_phase, dt, jobFence);
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
        m_GrassDataArray.Dispose();
    }

    Mesh CreateGrassBladeMesh()
    {
        if (m_mesh == null)
        {
            float fScale = 0.05f;
            float offset_x = 1.0f * fScale;
            float offset_y = 10.0f * fScale;
            Vector3 vertice_0 = new Vector3(offset_x, 0.0f, 0.0f);
            Vector3 vertice_1 = new Vector3(-offset_x, 0.0f, 0.0f);
            Vector3 vertice_2 = new Vector3(offset_x, offset_y, 0.0f);
            Vector3 vertice_3 = new Vector3(-offset_x, offset_y, 0.0f);
            Vector3 vertice_4 = new Vector3(offset_x, offset_y * 2.0f, 0.0f);
            Vector3 vertice_5 = new Vector3(-offset_x, offset_y * 2.0f, 0.0f);
            Vector3 vertice_6 = new Vector3(0.0f, offset_y * 3.0f, 0.0f);

            Vector3[] vertices =
            {
                vertice_0,
                vertice_1,
                vertice_2,
                vertice_3,
                vertice_4,
                vertice_5,
                vertice_6
            };

            int[] triangles =
            {
                0,1,2,
                2,1,3,
                2,3,4,
                4,3,5,
                4,5,6
            };

            m_mesh = new Mesh();
            m_mesh.vertices = vertices;
            m_mesh.triangles = triangles;
            return m_mesh;
        }
        else
        {
            return m_mesh;
        }
    }
}
