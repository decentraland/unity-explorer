using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Rendering.RenderSystem
{
    public class MaterialManager
    {
        internal static readonly int BASEMAP_ST_ID = Shader.PropertyToID("_BaseMap_ST");
        internal static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        internal static readonly int SPEC_COLOR_ID = Shader.PropertyToID("_SpecColor");
        internal static readonly int EMISSION_COLOR_ID = Shader.PropertyToID("_EmissionColor");
        internal static readonly int PLANE_CLIPPING_ID = Shader.PropertyToID("_PlaneClipping");
        internal static readonly int VERTICAL_CLIPPING_ID = Shader.PropertyToID("_VerticalClipping");
        internal static readonly int CUTOFF_ID = Shader.PropertyToID("_Cutoff");
        internal static readonly int SMOOTHNESS_ID = Shader.PropertyToID("_Smoothness");
        internal static readonly int METALLIC_ID = Shader.PropertyToID("_Metallic");
        internal static readonly int BUMP_SCALE_ID = Shader.PropertyToID("_BumpScale");
        internal static readonly int PARALLAX_ID = Shader.PropertyToID("_Parallax");
        internal static readonly int OCCLUSION_STRENGTH_ID = Shader.PropertyToID("_OcclusionStrength");
        internal static readonly int SURFACE_ID = Shader.PropertyToID("_Surface");
        internal static readonly int RSUV_BUFFER_ID = Shader.PropertyToID("_GPUBuffer_PerRSUVMaterial");

        public struct PerRSUVMaterial
        {
            public Vector4 _BaseMap_ST;
            public Vector4 _BaseColor;
            public Vector4 _SpecColor;
            public Vector4 _EmissionColor;
            public Vector4 _PlaneClipping;
            public Vector4 _VerticalClipping;
            public float _Cutoff;
            public float _Smoothness;
            public float _Metallic;
            public float _BumpScale;
            public float _Parallax;
            public float _OcclusionStrength;
            public float _Surface;
            public float _padding;
        }

        private GraphicsBuffer GPUBuffer_PerMaterial;
        private UInt16 currentPosition = 0;
        private NativeList<PerRSUVMaterial> perMaterials;
        PerRSUVMaterial perMat = new PerRSUVMaterial();
        private Dictionary<uint, Material> materialDictionary = new Dictionary<uint, Material>(512);
        private UInt32 nMaterialCount = 0;
        private int nFrameGPUStartingPosition = 0;

        public MaterialManager()
        {
            GPUBuffer_PerMaterial = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, UInt16.MaxValue, Marshal.SizeOf(typeof(PerRSUVMaterial)));
            Shader.SetGlobalBuffer(RSUV_BUFFER_ID, GPUBuffer_PerMaterial);
            perMaterials = new NativeList<PerRSUVMaterial>(1024,Allocator.Persistent);
        }

        public void AddMaterial(Renderer renderer)
        {
            Material newMaterial = SRUV_Adjustment(renderer);
            uint matCRC = MaterialCRC.ComputeCustomMaterialCRC(newMaterial);
            if (materialDictionary.TryGetValue(matCRC, out Material _mat))
            {
                renderer.sharedMaterials = new Material[] { _mat };
                UnityEngine.Object.Destroy(newMaterial);
            }
            else
            {
                newMaterial.name = "Material_" + ++nMaterialCount + "_" + matCRC;
                renderer.sharedMaterials = new Material[] { newMaterial };
                materialDictionary.Add(matCRC, newMaterial);
            }
        }

        private Material SRUV_Adjustment(Renderer renderer)
        {
            MeshRenderer meshRenderer = (MeshRenderer)renderer;
            Material mat = meshRenderer.sharedMaterial;
            perMat._BaseMap_ST = mat.GetVector(BASEMAP_ST_ID);
            perMat._BaseColor = mat.GetVector(BASE_COLOR_ID);
            perMat._SpecColor = mat.GetVector(SPEC_COLOR_ID);
            perMat._EmissionColor = mat.GetVector(EMISSION_COLOR_ID);
            perMat._PlaneClipping = mat.GetVector(PLANE_CLIPPING_ID);
            perMat._VerticalClipping = mat.GetVector(VERTICAL_CLIPPING_ID);
            perMat._Cutoff = mat.GetFloat(CUTOFF_ID);
            perMat._Smoothness = mat.GetFloat(SMOOTHNESS_ID);
            perMat._Metallic = mat.GetFloat(METALLIC_ID);
            perMat._BumpScale = mat.GetFloat(BUMP_SCALE_ID);
            perMat._Parallax = mat.GetFloat(PARALLAX_ID);
            perMat._OcclusionStrength = mat.GetFloat(OCCLUSION_STRENGTH_ID);
            perMat._Surface = mat.GetFloat(SURFACE_ID);
            perMat._padding = 0.0f;

            perMaterials.Add(perMat);

            meshRenderer.SetShaderUserValue(currentPosition++);

            Material newMaterial = new Material(mat);
            newMaterial.enableInstancing = true;
            newMaterial.SetVector(BASEMAP_ST_ID, Vector4.zero);
            newMaterial.SetVector(BASE_COLOR_ID, Vector4.one);
            newMaterial.SetVector(SPEC_COLOR_ID, Vector4.zero);
            newMaterial.SetVector(EMISSION_COLOR_ID, Vector4.zero);
            // newMaterial.SetVector(PLANE_CLIPPING_ID, Vector4.zero);
            // newMaterial.SetVector(VERTICAL_CLIPPING_ID, Vector4.zero);
            newMaterial.SetFloat(CUTOFF_ID, 0.0f);
            newMaterial.SetFloat(SMOOTHNESS_ID, 0.0f);
            newMaterial.SetFloat(METALLIC_ID, 0.0f);
            newMaterial.SetFloat(BUMP_SCALE_ID, 0.0f);
            newMaterial.SetFloat(PARALLAX_ID, 0.0f);
            newMaterial.SetFloat(OCCLUSION_STRENGTH_ID, 0.0f);
            newMaterial.EnableKeyword("_RSUV");
            // newMaterial.SetFloat("_Surface", 0.0f);
            // newMaterial.SetInt("_Cull", 2);
            // newMaterial.renderQueue = (int)(RenderQueue.Geometry + 50);

            return newMaterial;
        }

        public void EndOfFramePushtoGPU()
        {
            if (perMaterials.Length > 0)
            {
                // Check if hitting capacity issues
                if (perMaterials.Length > perMaterials.Capacity * 0.9f)
                {
                    Debug.LogWarning($"PerMaterial NativeList near capacity: {perMaterials.Length}/{perMaterials.Capacity}");
                }
                GPUBuffer_PerMaterial.SetData(perMaterials.AsArray(), 0, nFrameGPUStartingPosition, perMaterials.Length);
                nFrameGPUStartingPosition += perMaterials.Length;
                perMaterials.Clear();
            }
        }

        public void Dispose()
        {
            if (perMaterials.IsCreated)
                perMaterials.Dispose();

            GPUBuffer_PerMaterial?.Dispose();
        }

        private void DefaultNonRequiredMaterialValues()
        {

        }
    }
}
