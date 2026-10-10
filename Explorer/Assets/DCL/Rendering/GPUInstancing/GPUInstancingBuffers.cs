using System;
using UnityEngine;

namespace DCL.Rendering.GPUInstancing
{
    public class GPUInstancingBuffers : IDisposable
    {
        public GraphicsBuffer LODLevels;
        public GraphicsBuffer InstanceLookUpAndDither;
        public GraphicsBuffer PerInstanceMatrices;
        public GraphicsBuffer GroupData;
        public GraphicsBuffer ArrLODCount;

        public GraphicsBuffer DrawArgs;
        public GraphicsBuffer.IndirectDrawIndexedArgs[] DrawArgsCommandData;

        public void Dispose()
        {
            LODLevels?.Dispose();
            LODLevels = null;

            InstanceLookUpAndDither?.Dispose();
            InstanceLookUpAndDither = null;

            PerInstanceMatrices?.Dispose();
            PerInstanceMatrices = null;

            GroupData?.Dispose();
            GroupData = null;

            ArrLODCount?.Dispose();
            ArrLODCount = null;

            DrawArgs?.Dispose();
            DrawArgs = null;
        }
    }
}
