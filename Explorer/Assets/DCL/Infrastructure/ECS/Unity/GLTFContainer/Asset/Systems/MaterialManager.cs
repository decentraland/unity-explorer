using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Rendering.RenderSystem
{
    public class MaterialManager
    {
        public struct PerRSUVMaterial
        {
            public Vector4 _BaseMap_ST;
            public Vector4 _BaseColor;
            //public Vector4 _SpecColor;
            public Vector4 _EmissionColor;
            public Vector4 _PlaneClipping;
            public Vector4 _VerticalClipping;
            public float _Cutoff;
            public float _Smoothness;
            public float _Metallic;
            //public float _BumpScale;
            //public float _Parallax;
            //public float _OcclusionStrength;
            //public float _Surface;
            public float _padding;
        }

        private GraphicsBuffer GPUBuffer_PerMaterial;
        private UInt16 currentPosition = 0;
        private List<PerRSUVMaterial> perMaterials;
        PerRSUVMaterial perMat = new PerRSUVMaterial();
        private SortedDictionary<uint, Material> materialSortedDictionary = new SortedDictionary<uint, Material>();

        public static readonly int RSUV_PerMaterialBuffer = Shader.PropertyToID("_GPUBuffer_PerRSUVMaterial");

        public MaterialManager()
        {
            GPUBuffer_PerMaterial = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, UInt16.MaxValue, Marshal.SizeOf(typeof(PerRSUVMaterial)));
            Shader.SetGlobalBuffer(RSUV_PerMaterialBuffer, GPUBuffer_PerMaterial);
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
                // var keywords = newMaterial.shaderKeywords;
                // System.Array.Sort(keywords); // Ensure consistent ordering
                // String debugOutput = "New Material Keywords: " + string.Join(", ", keywords);
                // Debug.Log($"{debugOutput}");
                renderer.sharedMaterials = new Material[] { newMaterial };
                materialSortedDictionary.Add(matCRC, newMaterial);
            }
        }

        private Material SRUV_Adjustment(Renderer renderer)
        {
            //PerMaterial perMat = new PerMaterial();
            perMat._BaseMap_ST = renderer.sharedMaterial.GetVector("_BaseMap_ST");
            perMat._BaseColor = renderer.sharedMaterial.GetVector("_BaseColor");
            //perMat._SpecColor = renderer.sharedMaterial.GetVector("_SpecColor");
            perMat._EmissionColor = Vector4.zero;//renderer.sharedMaterial.GetVector("_EmissionColor");
            perMat._PlaneClipping = renderer.sharedMaterial.GetVector("_PlaneClipping");
            perMat._VerticalClipping = renderer.sharedMaterial.GetVector("_VerticalClipping");
            perMat._Cutoff = renderer.sharedMaterial.GetFloat("_Cutoff");
            perMat._Smoothness = renderer.sharedMaterial.GetFloat("_Smoothness");
            perMat._Metallic = renderer.sharedMaterial.GetFloat("_Metallic");
            // perMat._BumpScale = renderer.sharedMaterial.GetFloat("_BumpScale");
            // perMat._Parallax = renderer.sharedMaterial.GetFloat("_Parallax");
            // perMat._OcclusionStrength = renderer.sharedMaterial.GetFloat("_OcclusionStrength");
            // perMat._Surface = renderer.sharedMaterial.GetFloat("_Surface");
            perMat._padding = 0.0f;

            //perMaterials.Clear();
            perMaterials.Add(perMat);

            // try
            // {
            //     GPUBuffer_PerMaterial.SetData(perMaterials, 0, currentPosition, count: 1);
            // }
            // catch (System.Exception e)
            // {
            //     Debug.LogError($"SetData failed at position {currentPosition}: {e.Message}");
            // }

            Debug.Log($"Buffer capacity: {GPUBuffer_PerMaterial.count}");
            Debug.Log($"{currentPosition}");
            ((MeshRenderer)renderer).SetShaderUserValue(currentPosition++);

            Material newMaterial = new Material(renderer.sharedMaterial);
            newMaterial.enableInstancing = true;
            newMaterial.SetVector("_BaseMap_ST", Vector4.zero);
            newMaterial.SetVector("_BaseColor", Vector4.one);
            newMaterial.SetVector("_SpecColor", Vector4.zero);
            newMaterial.SetVector("_EmissionColor", Vector4.zero);
            newMaterial.SetVector("_PlaneClipping", Vector4.zero);
            newMaterial.SetVector("_VerticalClipping", Vector4.zero);
            newMaterial.SetFloat("_Cutoff", 0.0f);
            newMaterial.SetFloat("_Smoothness", 0.0f);
            newMaterial.SetFloat("_Metallic", 0.0f);
            newMaterial.SetFloat("_BumpScale", 0.0f);
            newMaterial.SetFloat("_Parallax", 0.0f);
            newMaterial.SetFloat("_OcclusionStrength", 0.0f);
            newMaterial.SetFloat("_Surface", 0.0f);
            newMaterial.SetInt("_Cull", 2);
            newMaterial.renderQueue = (int)(RenderQueue.Geometry + 50);

            return newMaterial;
        }

        public void EndOFFramePUSHtoGPU()
        {
            GPUBuffer_PerMaterial.SetData(perMaterials.ToArray(), 0, 0, perMaterials.Count);
            //perMaterials.Clear();
        }

        private void DefaultNonRequiredMaterialValues()
        {

        }
    }
}
