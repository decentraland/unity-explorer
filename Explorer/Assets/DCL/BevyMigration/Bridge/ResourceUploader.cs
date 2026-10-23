using DCL.BevyMigration.Bridge.Generated;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DCL.BevyMigration.Bridge
{
    /// <summary>
    ///     Copies meshes, textures and materials to the bridge once each (BevyMigration/docs/README.md § 8
    ///     "Resource transfer", phase 1) and hands out their handles. Keyed by Unity object identity; resources stay
    ///     resident for the session (release is step 5's budget work).
    /// </summary>
    public unsafe class ResourceUploader
    {
        private const uint FIRST_HANDLE = 1;
        private const int HEADER_SIZE = 5 * sizeof(uint);
        private const uint FORMAT_RGBA8 = 0;
        private const string FAMILY_UNLIT_TOKEN = "Unlit";

        private static readonly ReportData REPORT_DATA = new (ReportCategory.ALWAYS);
        private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
        private static readonly int COLOR = Shader.PropertyToID("_Color");
        private static readonly int BASE_MAP = Shader.PropertyToID("_BaseMap");
        private static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");
        private static readonly int METALLIC = Shader.PropertyToID("_Metallic");
        private static readonly int SMOOTHNESS = Shader.PropertyToID("_Smoothness");
        private static readonly int METALLIC_GLOSS_MAP = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int BUMP_MAP = Shader.PropertyToID("_BumpMap");
        private static readonly int BUMP_SCALE = Shader.PropertyToID("_BumpScale");
        private static readonly int OCCLUSION_MAP = Shader.PropertyToID("_OcclusionMap");
        private static readonly int OCCLUSION_STRENGTH = Shader.PropertyToID("_OcclusionStrength");
        private static readonly int EMISSION_COLOR = Shader.PropertyToID("_EmissionColor");
        private static readonly int EMISSION_MAP = Shader.PropertyToID("_EmissionMap");
        private static readonly int SURFACE = Shader.PropertyToID("_Surface");
        private static readonly int ALPHA_CLIP = Shader.PropertyToID("_AlphaClip");
        private static readonly int CUTOFF = Shader.PropertyToID("_Cutoff");
        private static readonly int CULL = Shader.PropertyToID("_Cull");

        private readonly Dictionary<EntityId, uint> meshHandles = new ();
        private readonly Dictionary<EntityId, uint> textureHandles = new ();
        private readonly Dictionary<EntityId, uint> materialHandles = new ();
        private readonly HashSet<EntityId> rejected = new ();
        private readonly List<byte> payload = new (1 << 16);

        private uint nextMesh = FIRST_HANDLE;
        private uint nextTexture = FIRST_HANDLE;
        private uint nextMaterial = FIRST_HANDLE;

        public int MeshCount => meshHandles.Count;

        public int TextureCount => textureHandles.Count;

        public int MaterialCount => materialHandles.Count;

        /// <summary>
        ///     The bridge's handle for the mesh, uploading it on first sight; <see cref="BridgeMeshes.PLACEHOLDER_CUBE" />
        ///     when the mesh cannot be read.
        /// </summary>
        public uint Mesh(Mesh mesh)
        {
            EntityId id = mesh.GetEntityId();

            if (meshHandles.TryGetValue(id, out uint handle)) return handle;
            if (rejected.Contains(id)) return BridgeMeshes.PLACEHOLDER_CUBE;

            handle = nextMesh;

            if (!TryUploadMesh(mesh, handle))
            {
                rejected.Add(id);
                return BridgeMeshes.PLACEHOLDER_CUBE;
            }

            nextMesh++;
            meshHandles[id] = handle;
            return handle;
        }

        /// <summary>
        ///     The bridge's handle for the material, uploading it and its readable textures on first sight.
        /// </summary>
        public uint Material(Material material)
        {
            EntityId id = material.GetEntityId();

            if (materialHandles.TryGetValue(id, out uint handle)) return handle;

            handle = nextMaterial++;
            BmMaterial blob = Describe(material);

            if (BevyBridge.UploadMaterial(handle, ref blob) != BmResult.Ok)
                ReportHub.LogWarning(REPORT_DATA, $"material upload refused for {material.name}");

            materialHandles[id] = handle;
            return handle;
        }

        private uint Texture(Texture? texture, bool srgb)
        {
            if (texture is not Texture2D texture2D) return BridgeTextures.NONE;

            EntityId id = texture2D.GetEntityId();

            if (textureHandles.TryGetValue(id, out uint handle)) return handle;
            if (rejected.Contains(id)) return BridgeTextures.NONE;

            handle = nextTexture;

            if (!TryUploadTexture(texture2D, srgb, handle))
            {
                rejected.Add(id);
                return BridgeTextures.NONE;
            }

            nextTexture++;
            textureHandles[id] = handle;
            return handle;
        }

        private bool TryUploadMesh(Mesh mesh, uint handle)
        {
            if (!mesh.isReadable && !Application.isEditor)
            {
                ReportHub.LogWarning(REPORT_DATA, $"mesh {mesh.name} is not readable; GPU readback of meshes lands with step 5");
                return false;
            }

            using Mesh.MeshDataArray dataArray = UnityEngine.Mesh.AcquireReadOnlyMeshData(mesh);
            Mesh.MeshData data = dataArray[0];
            int vertexCount = data.vertexCount;
            int submeshCount = data.subMeshCount;
            if (vertexCount == 0 || submeshCount == 0) return false;

            bool hasNormals = data.HasVertexAttribute(VertexAttribute.Normal);
            bool hasTangents = data.HasVertexAttribute(VertexAttribute.Tangent);
            bool hasUv0 = data.HasVertexAttribute(VertexAttribute.TexCoord0);
            bool hasUv1 = data.HasVertexAttribute(VertexAttribute.TexCoord1);
            bool hasColors = data.HasVertexAttribute(VertexAttribute.Color);
            bool indices32 = data.indexFormat == IndexFormat.UInt32;

            var attributes = 0u;
            if (hasNormals) attributes |= BridgeMeshAttributes.NORMAL;
            if (hasTangents) attributes |= BridgeMeshAttributes.TANGENT;
            if (hasUv0) attributes |= BridgeMeshAttributes.UV0;
            if (hasUv1) attributes |= BridgeMeshAttributes.UV1;
            if (hasColors) attributes |= BridgeMeshAttributes.COLOR;

            var indexCount = 0;
            for (var i = 0; i < submeshCount; i++) indexCount += (int)data.GetSubMesh(i).indexCount;

            payload.Clear();
            WriteUInt((uint)vertexCount);
            WriteUInt((uint)indexCount);
            WriteUInt((uint)submeshCount);
            WriteUInt(attributes);
            WriteUInt(indices32 ? BridgeIndexFormat.U32 : BridgeIndexFormat.U16);

            using (var positions = new NativeArray<Vector3>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                data.GetVertices(positions);
                WriteFloats((float*)positions.GetUnsafeReadOnlyPtr(), vertexCount * 3);
            }

            if (hasNormals)
                using (var normals = new NativeArray<Vector3>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
                {
                    data.GetNormals(normals);
                    WriteFloats((float*)normals.GetUnsafeReadOnlyPtr(), vertexCount * 3);
                }

            if (hasTangents)
                using (var tangents = new NativeArray<Vector4>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
                {
                    data.GetTangents(tangents);
                    WriteFloats((float*)tangents.GetUnsafeReadOnlyPtr(), vertexCount * 4);
                }

            if (hasUv0)
                using (var uv = new NativeArray<Vector2>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
                {
                    data.GetUVs(0, uv);
                    WriteFloats((float*)uv.GetUnsafeReadOnlyPtr(), vertexCount * 2);
                }

            if (hasUv1)
                using (var uv = new NativeArray<Vector2>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
                {
                    data.GetUVs(1, uv);
                    WriteFloats((float*)uv.GetUnsafeReadOnlyPtr(), vertexCount * 2);
                }

            if (hasColors)
                using (var colors = new NativeArray<Color>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
                {
                    data.GetColors(colors);
                    WriteFloats((float*)colors.GetUnsafeReadOnlyPtr(), vertexCount * 4);
                }

            // Indices follow submesh by submesh so each submesh is a contiguous range of the index stream.
            if (indices32)
            {
                NativeArray<uint> indices = data.GetIndexData<uint>();
                for (var i = 0; i < submeshCount; i++)
                {
                    SubMeshDescriptor submesh = data.GetSubMesh(i);
                    WriteBytes((byte*)((uint*)indices.GetUnsafeReadOnlyPtr() + submesh.indexStart), submesh.indexCount * sizeof(uint));
                }
            }
            else
            {
                NativeArray<ushort> indices = data.GetIndexData<ushort>();
                for (var i = 0; i < submeshCount; i++)
                {
                    SubMeshDescriptor submesh = data.GetSubMesh(i);
                    WriteBytes((byte*)((ushort*)indices.GetUnsafeReadOnlyPtr() + submesh.indexStart), submesh.indexCount * sizeof(ushort));
                }
            }

            var start = 0u;
            for (var i = 0; i < submeshCount; i++)
            {
                SubMeshDescriptor submesh = data.GetSubMesh(i);
                WriteUInt(start);
                WriteUInt((uint)submesh.indexCount);
                start += (uint)submesh.indexCount;
            }

            return Send(BmResourceCmdKind.MeshUpload, handle, mesh.name);
        }

        private bool TryUploadTexture(Texture2D texture, bool srgb, uint handle)
        {
            bool mapped = BridgeTextureFormats.TryMap(texture.format, out uint format);

            if (texture.isReadable && mapped)
            {
                NativeArray<byte> raw = texture.GetRawTextureData<byte>();
                WriteTextureHeader(texture, format, srgb);
                WriteBytes((byte*)raw.GetUnsafeReadOnlyPtr(), raw.Length);
                return Send(BmResourceCmdKind.TextureUpload, handle, texture.name);
            }

            // Asset-bundle textures are GPU-only and AsyncGPUReadback rejects compressed sources, so the texture is
            // blitted into an RGBA8 render texture (sRGB-encoded when the source is) whose mip chain is read back.
            if (TryReadBack(texture, srgb))
                return Send(BmResourceCmdKind.TextureUpload, handle, texture.name);

            ReportHub.LogWarning(REPORT_DATA, $"texture {texture.name} ({texture.format}) could not be read back");
            return false;
        }

        private bool TryReadBack(Texture2D texture, bool srgb)
        {
            var descriptor = new RenderTextureDescriptor(texture.width, texture.height, srgb ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm, 0)
            {
                useMipMap = texture.mipmapCount > 1,
                autoGenerateMips = true,
                msaaSamples = 1,
            };

            RenderTexture target = RenderTexture.GetTemporary(descriptor);
            Graphics.Blit(texture, target);
            WriteTextureHeader(texture, FORMAT_RGBA8, srgb);
            var complete = true;

            for (var mip = 0; mip < texture.mipmapCount && complete; mip++)
            {
                AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(target, mip);
                request.WaitForCompletion();
                complete = !request.hasError;

                if (complete)
                {
                    NativeArray<byte> data = request.GetData<byte>();
                    WriteBytes((byte*)data.GetUnsafeReadOnlyPtr(), data.Length);
                }
            }

            RenderTexture.ReleaseTemporary(target);
            return complete;
        }

        private void WriteTextureHeader(Texture2D texture, uint format, bool srgb)
        {
            payload.Clear();
            WriteUInt((uint)texture.width);
            WriteUInt((uint)texture.height);
            WriteUInt((uint)texture.mipmapCount);
            WriteUInt(format);
            WriteUInt(srgb ? 1u : 0u);
            WriteUInt(BridgeTextureFormats.Wrap(texture.wrapModeU));
            WriteUInt(BridgeTextureFormats.Wrap(texture.wrapModeV));
            WriteUInt(BridgeTextureFormats.Filter(texture.filterMode));
        }

        private BmMaterial Describe(Material material)
        {
            bool unlit = material.shader.name.Contains(FAMILY_UNLIT_TOKEN, StringComparison.Ordinal);
            Color baseColor = material.HasProperty(BASE_COLOR) ? material.GetColor(BASE_COLOR) : material.HasProperty(COLOR) ? material.GetColor(COLOR) : Color.white;
            Color linear = baseColor.linear;
            Color emission = material.HasProperty(EMISSION_COLOR) ? material.GetColor(EMISSION_COLOR).linear : Color.black;
            float surface = material.HasProperty(SURFACE) ? material.GetFloat(SURFACE) : 0f;
            bool alphaClip = material.HasProperty(ALPHA_CLIP) && material.GetFloat(ALPHA_CLIP) > 0.5f;
            float cull = material.HasProperty(CULL) ? material.GetFloat(CULL) : (float)CullMode.Back;
            Texture? baseMap = material.HasProperty(BASE_MAP) ? material.GetTexture(BASE_MAP) : material.HasProperty(MAIN_TEX) ? material.GetTexture(MAIN_TEX) : null;
            Vector4 st = material.HasProperty(BASE_MAP) ? material.GetTextureScale(BASE_MAP).ToScaleOffset(material.GetTextureOffset(BASE_MAP)) : new Vector4(1f, 1f, 0f, 0f);

            var blob = new BmMaterial
            {
                family = unlit ? BmMaterialFamily.Unlit : BmMaterialFamily.UrpLit,
                alpha_mode = alphaClip ? BmAlphaMode.Cutout : surface > 0.5f ? BmAlphaMode.Transparent : BmAlphaMode.Opaque,
                double_sided = cull < 0.5f ? 1u : 0u,
                scene_bounds_enabled = 0,
                metallic = material.HasProperty(METALLIC) ? material.GetFloat(METALLIC) : 0f,
                smoothness = material.HasProperty(SMOOTHNESS) ? material.GetFloat(SMOOTHNESS) : 0.5f,
                alpha_cutoff = material.HasProperty(CUTOFF) ? material.GetFloat(CUTOFF) : 0.5f,
                normal_scale = material.HasProperty(BUMP_SCALE) ? material.GetFloat(BUMP_SCALE) : 1f,
                occlusion_strength = material.HasProperty(OCCLUSION_STRENGTH) ? material.GetFloat(OCCLUSION_STRENGTH) : 1f,
                base_map = Texture(baseMap, true),
                normal_map = material.HasProperty(BUMP_MAP) ? Texture(material.GetTexture(BUMP_MAP), false) : BridgeTextures.NONE,
                metallic_gloss_map = material.HasProperty(METALLIC_GLOSS_MAP) ? Texture(material.GetTexture(METALLIC_GLOSS_MAP), false) : BridgeTextures.NONE,
                occlusion_map = material.HasProperty(OCCLUSION_MAP) ? Texture(material.GetTexture(OCCLUSION_MAP), false) : BridgeTextures.NONE,
                emission_map = material.HasProperty(EMISSION_MAP) ? Texture(material.GetTexture(EMISSION_MAP), true) : BridgeTextures.NONE,
            };

            blob.base_color[0] = linear.r;
            blob.base_color[1] = linear.g;
            blob.base_color[2] = linear.b;
            blob.base_color[3] = linear.a;
            blob.emission[0] = emission.r;
            blob.emission[1] = emission.g;
            blob.emission[2] = emission.b;
            blob.base_map_st[0] = st.x;
            blob.base_map_st[1] = st.y;
            blob.base_map_st[2] = st.z;
            blob.base_map_st[3] = st.w;
            return blob;
        }

        private bool Send(BmResourceCmdKind kind, uint handle, string name)
        {
            byte[] bytes = payload.ToArray();

            fixed (byte* ptr = bytes)
            {
                var command = new BmResourceCmd { kind = kind, handle = handle, data = ptr, len = (nuint)bytes.Length };
                BmResult result = BevyBridge.Resource(ref command);

                if (result != BmResult.Ok)
                {
                    ReportHub.LogWarning(REPORT_DATA, $"{kind} refused for {name}: {result}");
                    return false;
                }
            }

            return true;
        }

        private void WriteUInt(uint value)
        {
            payload.Add((byte)value);
            payload.Add((byte)(value >> 8));
            payload.Add((byte)(value >> 16));
            payload.Add((byte)(value >> 24));
        }

        private void WriteFloats(float* values, int count) =>
            WriteBytes((byte*)values, count * sizeof(float));

        private void WriteBytes(byte* bytes, int count)
        {
            for (var i = 0; i < count; i++)
                payload.Add(bytes[i]);
        }
    }

    /// <summary>Mirrors `abi::mesh_attributes`.</summary>
    public static class BridgeMeshAttributes
    {
        public const uint NORMAL = 1;
        public const uint TANGENT = 2;
        public const uint UV0 = 4;
        public const uint UV1 = 8;
        public const uint COLOR = 16;
    }

    /// <summary>Mirrors `abi::index_format`.</summary>
    public static class BridgeIndexFormat
    {
        public const uint U16 = 0;
        public const uint U32 = 1;
    }

    /// <summary>Mirrors `abi::TEXTURE_NONE` and the texture header enums.</summary>
    public static class BridgeTextures
    {
        public const uint NONE = 0;
    }

    public static class BridgeTextureFormats
    {
        /// <summary>Maps a Unity format to `abi::BmTextureFormat`; false when the bridge has no equivalent.</summary>
        public static bool TryMap(TextureFormat format, out uint mapped)
        {
            switch (format)
            {
                case TextureFormat.RGBA32: mapped = 0; return true;
                case TextureFormat.BGRA32: mapped = 1; return true;
                case TextureFormat.DXT1: mapped = 2; return true;
                case TextureFormat.DXT5: mapped = 3; return true;
                case TextureFormat.BC4: mapped = 4; return true;
                case TextureFormat.BC5: mapped = 5; return true;
                case TextureFormat.BC7: mapped = 6; return true;
                case TextureFormat.R8: mapped = 7; return true;
                case TextureFormat.RG16: mapped = 8; return true;
                default: mapped = 0; return false;
            }
        }

        public static uint Wrap(TextureWrapMode mode) =>
            mode switch
            {
                TextureWrapMode.Clamp => 1,
                TextureWrapMode.Mirror or TextureWrapMode.MirrorOnce => 2,
                _ => 0,
            };

        public static uint Filter(FilterMode mode) =>
            mode switch
            {
                FilterMode.Point => 0,
                FilterMode.Bilinear => 1,
                _ => 2,
            };
    }

    internal static class VectorExtensions
    {
        public static Vector4 ToScaleOffset(this Vector2 scale, Vector2 offset) =>
            new (scale.x, scale.y, offset.x, offset.y);
    }
}
