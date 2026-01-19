using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        private List<PerRSUVMaterial> perMaterials;
        PerRSUVMaterial perMat = new PerRSUVMaterial();
        private SortedDictionary<uint, Material> materialSortedDictionary = new SortedDictionary<uint, Material>();
        private UInt32 nMaterialCount = 0;
        private int nFrameGPUStartingPosition = 0;

        public MaterialManager()
        {
            GPUBuffer_PerMaterial = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, UInt16.MaxValue, Marshal.SizeOf(typeof(PerRSUVMaterial)));
            Shader.SetGlobalBuffer(RSUV_BUFFER_ID, GPUBuffer_PerMaterial);
            perMaterials = new List<PerRSUVMaterial>();
        }

        public void AddMaterial(Renderer renderer)
        {
            Material newMaterial = SRUV_Adjustment(renderer);
            uint matCRC = MaterialCRC.ComputeCustomMaterialCRC(newMaterial);
            if (materialSortedDictionary.TryGetValue(matCRC, out Material _mat))
            {
                renderer.sharedMaterials = new Material[] { _mat };
                UnityEngine.Object.Destroy(newMaterial);
            }
            else
            {
                newMaterial.name = "Material_" + ++nMaterialCount + "_" + matCRC;
                renderer.sharedMaterials = new Material[] { newMaterial };
                materialSortedDictionary.Add(matCRC, newMaterial);
            }
        }

        private Material SRUV_Adjustment(Renderer renderer)
        {
            perMat._BaseMap_ST = renderer.sharedMaterial.GetVector(BASEMAP_ST_ID);
            perMat._BaseColor = renderer.sharedMaterial.GetVector(BASE_COLOR_ID);
            perMat._SpecColor = renderer.sharedMaterial.GetVector(SPEC_COLOR_ID);
            perMat._EmissionColor = renderer.sharedMaterial.GetVector(EMISSION_COLOR_ID);
            perMat._PlaneClipping = renderer.sharedMaterial.GetVector(PLANE_CLIPPING_ID);
            perMat._VerticalClipping = renderer.sharedMaterial.GetVector(VERTICAL_CLIPPING_ID);
            perMat._Cutoff = renderer.sharedMaterial.GetFloat(CUTOFF_ID);
            perMat._Smoothness = renderer.sharedMaterial.GetFloat(SMOOTHNESS_ID);
            perMat._Metallic = renderer.sharedMaterial.GetFloat(METALLIC_ID);
            perMat._BumpScale = renderer.sharedMaterial.GetFloat(BUMP_SCALE_ID);
            perMat._Parallax = renderer.sharedMaterial.GetFloat(PARALLAX_ID);
            perMat._OcclusionStrength = renderer.sharedMaterial.GetFloat(OCCLUSION_STRENGTH_ID);
            perMat._Surface = renderer.sharedMaterial.GetFloat(SURFACE_ID);
            perMat._padding = 0.0f;

            perMaterials.Add(perMat);

            ((MeshRenderer)renderer).SetShaderUserValue(currentPosition++);

            Material newMaterial = new Material(renderer.sharedMaterial);
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
            if (perMaterials.Count > 0)
            {
                GPUBuffer_PerMaterial.SetData(perMaterials.ToArray(), 0, nFrameGPUStartingPosition, perMaterials.Count);
                nFrameGPUStartingPosition += perMaterials.Count;
                perMaterials.Clear();
            }
        }

        private void DefaultNonRequiredMaterialValues()
        {

        }
    }
}
