using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Rendering.GPUDrivenInstanceBatcher
{
    public class GPUDrivenInstanceBatcher
    {
        private string profilerTag = "GPUDrivenInstanceBatcher";
        private GraphicsBuffer m_GPUPersistentInstanceData;
        private ComputeBuffer m_GPUPersistentInstanceDataBuffer;
        private ComputeBuffer drawArgsBuffer;
        private Mesh m_MeshInstance;
        private Material m_MaterialInstance;
        private Shader m_ShaderInstance;
        private ComputeShader m_CullingComputeShader;
        private Camera[] m_Cameras;

        private uint m_InstanceCount;

        public GPUDrivenInstanceBatcher(Mesh mesh, Material material)
        {
            this.profilerTag = profilerTag;
            this.renderPassEvent = renderPassEvent;
            this.brightsCompute = brightsCompute;
            this.flareMaterial = flareMaterial;

            // findBrightsKernel = brightsCompute.FindKernel("FindBrights");
            // sourceTextureID = Shader.PropertyToID("_sourceTexture");
            // brightQuadsID = Shader.PropertyToID("_brightPoints");
            // luminanceThresholdID = Shader.PropertyToID("_luminanceThreshold");
            // screenSizeXID = Shader.PropertyToID("_screenSizeX");
            // screenSizeYID = Shader.PropertyToID("_screenSizeY");
            // angleID = Shader.PropertyToID("_angle");
            // widthRatioID = Shader.PropertyToID("_widthRatio");
            // resolvedCameraColourID = Shader.PropertyToID("_resolvedCameraColour");

            // fetch the numthreads values of the kernel (assume z is 1 so ignore it)
            brightsCompute.GetKernelThreadGroupSizes(findBrightsKernel,
                out uint sizeX, out uint sizeY, out var _);
            groupSizeX = (int)sizeX;
            groupSizeY = (int)sizeY;

            // I can't find a good place to Dispose() these ComputeBuffers. When in editor mode
            // this pass can be recreated many times, leading to them being garbage collected and
            // triggering a warning.
            // They could be recreated every frame but I suspect that'll be slower with the only
            // apparent gain being to avoid a warning from Unity.

            // buffer size here is arbitrary, if hitting the max is likely consider picking which
            // bright points are culled by something not totally arbitrary.
            brightPoints = new ComputeBuffer(1000, sizeof(float) * 8, ComputeBufferType.Append);

            // a buffer used as draw arguments for an indirect call must be created as the IndirectArguments type.
            drawArgsBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);

            drawArgsBuffer.SetData(new uint[] {
                6, // vertices per instance
                0, // instance count (will be set from brightPoints counter)
                0, // byte offset of first vertex
                0, // byte offset of first instance
            });
        }

        public void CommitPersistentDataToGPUMemory()
        {

        }

        private void InitGPUPersistentInstanceData()
        {

        }

        private void Compute_CullingKernel()
        {
            // if not using MSAA we can directly read from the camera's render target
            cmd.SetComputeTextureParam(brightsCompute, findBrightsKernel, sourceTextureID, cameraColorIdent);

            // Compute shader to find brightest pixels, limited to one per group thread region.
            // When it finds a bright pixel record its details in the brightPoints buffer
            cmd.SetComputeBufferParam(brightsCompute, findBrightsKernel, brightQuadsID, brightPoints);
            cmd.SetComputeFloatParam(brightsCompute, luminanceThresholdID, luminanceThreshold);

            // calculation of thread groups ensures the whole screen is covered
            cmd.DispatchCompute(brightsCompute, findBrightsKernel,
                Mathf.CeilToInt(cameraTextureDescriptor.width / groupSizeX),
                Mathf.CeilToInt(cameraTextureDescriptor.height / groupSizeY),
                1
            );
        }

        private void IndirectDraw()
        {
            // fetch a command buffer to use
            CommandBuffer cmd = new CommandBuffer();
            //cmd.Clear();



            // put brightPoints count into instanceCount slot of drawArgsBuffer
            cmd.CopyCounterValue(brightPoints, drawArgsBuffer, sizeof(uint));

            // earlier resolve Blit may have changed render target, so set it back
            cmd.SetRenderTarget(cameraColorIdent);

            // draw the quads described by brightsCompute
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetBuffer(brightQuadsID, brightPoints);
            properties.SetFloat(angleID, angle);
            properties.SetFloat(widthRatioID, renderingData.cameraData.camera.aspect);
            properties.SetFloat(screenSizeXID, cameraTextureDescriptor.width);
            properties.SetFloat(screenSizeYID, cameraTextureDescriptor.height);

            // it would make sense to use MeshTopology.Quads as we're drawing quads, but Unity docs say:
            // "quad topology is emulated on many platforms, so it's more efficient to use a triangular mesh."
            cmd.DrawProceduralIndirect(Matrix4x4.identity, flareMaterial, 0, MeshTopology.Triangles, drawArgsBuffer, 0, properties);


        }
    }
}
