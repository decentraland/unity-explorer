using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace DCL.Rendering.GPUInstanceBatcher
{
    public class GPUInstanceBatcher : MonoBehaviour
    {
        private string profilerTag = "GPUInstanceBatcher";

        [NonSerialized]
        public Bounds instancingBounds;

        public bool isFrustumCulling = true;
        public bool isOcclusionCulling = true;
        public float maxCullingDistance = 0;

        public static bool showRenderedAmount;

        protected static ComputeShader _cameraComputeShader;
        protected static int[] _cameraComputeKernelIDs;

        protected static ComputeShader _visibilityComputeShader;
        protected static int[] _instanceVisibilityComputeKernelIDs;

        protected static ComputeShader _argsBufferComputeShader;
        protected static int _argsBufferDoubleInstanceCountComputeKernelID;



        // Timings
        public static int lastDrawCallFrame;
        public static float lastDrawCallTime;
        public static float timeSinceLastDrawCall;

        [NonSerialized]
        protected bool isInitial = true;

        [NonSerialized]
        public bool isInitialized = false;

        [NonSerialized]
        public bool isQuiting = false;

        public LayerMask layerMask = ~0;
        public bool lightProbeDisabled = false;

        public GPUInstanceBatcher(Mesh mesh, Material material)
        {

        }
    }
}
