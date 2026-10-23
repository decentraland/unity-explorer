using DCL.BevyMigration.Bridge.Generated;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.BevyMigration.Bridge
{
    /// <summary>
    ///     Builds one <see cref="BmSnapshot" /> per frame from the registry and the camera and submits it. The arrays
    ///     are unmanaged (`NativeList` / `NativeArray`); a single buffer is enough because the native side copies
    ///     everything out before `bm_submit_snapshot` returns.
    /// </summary>
    public unsafe class SnapshotWriter
    {
        private readonly RendererRegistry registry;

        private NativeArray<float4x4> worlds;
        private ulong frame;

        public SnapshotWriter(RendererRegistry registry)
        {
            this.registry = registry;
        }

        public ulong Frame => frame;

        public void Dispose()
        {
            if (worlds.IsCreated) worlds.Dispose();
        }

        /// <summary>
        ///     Schedules the transform gather for the current registry contents; the array it fills stays valid
        ///     until the next call.
        /// </summary>
        public JobHandle ScheduleGather()
        {
            int count = registry.Count;

            if (!worlds.IsCreated || worlds.Length < count)
            {
                if (worlds.IsCreated) worlds.Dispose();
                worlds = new NativeArray<float4x4>(math.max(count, 64), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            if (count == 0) return default(JobHandle);

            var job = new TransformGatherJob { Worlds = worlds };
            return job.Schedule(registry.Transforms);
        }

        /// <summary>
        ///     Writes the camera, lighting and renderer arrays and submits the frame. Call after the gather job completed.
        /// </summary>
        public BmResult Submit(Camera camera)
        {
            frame++;
            int count = registry.Count;
            Light? sun = RenderSettings.sun;
            Color ambient = RenderSettings.ambientLight.linear;

            var snapshot = new BmSnapshot
            {
                frame = frame,
                renderer_count = (uint)count,
                handles = count > 0 ? (uint*)registry.Handles.GetUnsafeReadOnlyPtr() : null,
                worlds = count > 0 ? (BmMat4*)worlds.GetUnsafeReadOnlyPtr() : null,
                meshes = count > 0 ? (uint*)registry.Meshes.GetUnsafeReadOnlyPtr() : null,
                flags = count > 0 ? (ushort*)registry.Flags.GetUnsafeReadOnlyPtr() : null,
            };

            WriteMatrix(camera.worldToCameraMatrix, snapshot.camera.view.cols);
            WriteMatrix(camera.projectionMatrix, snapshot.camera.projection.cols);
            snapshot.camera.near = camera.nearClipPlane;
            snapshot.camera.far = camera.farClipPlane;
            snapshot.camera.fov_degrees = camera.orthographic ? 0f : camera.fieldOfView;

            snapshot.ambient[0] = ambient.r;
            snapshot.ambient[1] = ambient.g;
            snapshot.ambient[2] = ambient.b;

            if (sun != null)
            {
                Vector3 direction = sun.transform.forward;
                Color color = sun.color.linear * sun.intensity;
                snapshot.sun_direction[0] = direction.x;
                snapshot.sun_direction[1] = direction.y;
                snapshot.sun_direction[2] = direction.z;
                snapshot.sun_color[0] = color.r;
                snapshot.sun_color[1] = color.g;
                snapshot.sun_color[2] = color.b;
            }
            else
            {
                snapshot.sun_direction[1] = -1f;
                snapshot.sun_color[0] = snapshot.sun_color[1] = snapshot.sun_color[2] = 1f;
            }

            return BevyBridge.Submit(ref snapshot);
        }

        private static void WriteMatrix(Matrix4x4 matrix, float* cols)
        {
            // Matrix4x4 stores columns contiguously (m00, m10, m20, m30, m01, ...), the layout BmMat4 declares.
            for (var i = 0; i < 16; i++)
                cols[i] = matrix[i];
        }
    }
}
