using DCL.BevyMigration.Bridge.Generated;
using System.Collections.Generic;
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
        private const int MAX_LIGHTS = 256;

        private readonly RendererRegistry registry;

        private NativeArray<float4x4> worlds;
        private NativeArray<BmLight> lights;
        private readonly Vector3[] ambientDirections = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        private readonly Color[] ambientSamples = new Color[6];
        private ulong frame;

        public SnapshotWriter(RendererRegistry registry)
        {
            this.registry = registry;
            lights = new NativeArray<BmLight>(MAX_LIGHTS, Allocator.Persistent);
        }

        public ulong Frame => frame;

        public void Dispose()
        {
            if (worlds.IsCreated) worlds.Dispose();
            if (lights.IsCreated) lights.Dispose();
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
        ///     Writes the camera, lighting, fog, renderer and material arrays and submits the frame. Call after the
        ///     gather job completed. <paramref name="sceneLights" /> are the point and spot lights of the loaded scenes.
        /// </summary>
        public BmResult Submit(Camera camera, Light? sun, IReadOnlyList<Light> sceneLights)
        {
            frame++;
            registry.RebuildMaterialTable();
            int count = registry.Count;
            Color ambient = AverageAmbient();
            int lightCount = WriteLights(sceneLights);

            var snapshot = new BmSnapshot
            {
                frame = frame,
                renderer_count = (uint)count,
                handles = count > 0 ? (uint*)registry.Handles.GetUnsafeReadOnlyPtr() : null,
                worlds = count > 0 ? (BmMat4*)worlds.GetUnsafeReadOnlyPtr() : null,
                meshes = count > 0 ? (uint*)registry.Meshes.GetUnsafeReadOnlyPtr() : null,
                flags = count > 0 ? (ushort*)registry.Flags.GetUnsafeReadOnlyPtr() : null,
                material_first = count > 0 ? (uint*)registry.MaterialFirst.GetUnsafeReadOnlyPtr() : null,
                material_count = count > 0 ? (byte*)registry.MaterialCount.GetUnsafeReadOnlyPtr() : null,
                material_handle_count = (uint)registry.MaterialHandles.Length,
                material_handles = registry.MaterialHandles.Length > 0 ? (uint*)registry.MaterialHandles.GetUnsafeReadOnlyPtr() : null,
                light_count = (uint)lightCount,
                lights = lightCount > 0 ? (BmLight*)lights.GetUnsafeReadOnlyPtr() : null,
            };

            snapshot.fog.mode = RenderSettings.fog ? (uint)RenderSettings.fogMode : 0u;
            Color fogColor = RenderSettings.fogColor.linear;
            snapshot.fog.color[0] = fogColor.r;
            snapshot.fog.color[1] = fogColor.g;
            snapshot.fog.color[2] = fogColor.b;
            snapshot.fog.density = RenderSettings.fogDensity;
            snapshot.fog.start = RenderSettings.fogStartDistance;
            snapshot.fog.end = RenderSettings.fogEndDistance;

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

        /// <summary>
        ///     The ambient probe averaged over the six axes: one value for every ambient mode (flat, trilight, skybox).
        /// </summary>
        private Color AverageAmbient()
        {
            RenderSettings.ambientProbe.Evaluate(ambientDirections, ambientSamples);
            Color sum = Color.black;

            for (var i = 0; i < ambientSamples.Length; i++)
                sum += ambientSamples[i];

            return sum / ambientSamples.Length;
        }

        private int WriteLights(IReadOnlyList<Light> sceneLights)
        {
            int written = Mathf.Min(sceneLights.Count, MAX_LIGHTS);

            for (var i = 0; i < written; i++)
            {
                Light light = sceneLights[i];
                Transform transform = light.transform;
                Vector3 position = transform.position;
                Vector3 direction = transform.forward;
                Color color = light.color.linear * light.intensity;

                var entry = new BmLight
                {
                    kind = light.type == LightType.Spot ? BridgeLightKinds.SPOT : BridgeLightKinds.POINT,
                    shadows = light.shadows != LightShadows.None ? 1u : 0u,
                    range = light.range,
                    spot_angle = light.spotAngle,
                };

                entry.position[0] = position.x;
                entry.position[1] = position.y;
                entry.position[2] = position.z;
                entry.direction[0] = direction.x;
                entry.direction[1] = direction.y;
                entry.direction[2] = direction.z;
                entry.color[0] = color.r;
                entry.color[1] = color.g;
                entry.color[2] = color.b;
                lights[i] = entry;
            }

            return written;
        }

        private static void WriteMatrix(Matrix4x4 matrix, float* cols)
        {
            // Matrix4x4 stores columns contiguously (m00, m10, m20, m30, m01, ...), the layout BmMat4 declares.
            for (var i = 0; i < 16; i++)
                cols[i] = matrix[i];
        }
    }

    /// <summary>Mirrors `abi::light_kind`.</summary>
    public static class BridgeLightKinds
    {
        public const uint DIRECTIONAL = 0;
        public const uint POINT = 1;
        public const uint SPOT = 2;
    }
}
