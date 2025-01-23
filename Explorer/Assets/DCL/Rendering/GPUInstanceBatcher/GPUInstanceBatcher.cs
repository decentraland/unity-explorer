using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Threading;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Rendering.GPUInstanceBatcher
{
    public class GPUInstanceBatcher : MonoBehaviour
    {
        private string profilerTag = "GPUDrivenInstanceBatcher";
        public List<GPUInstanceBatcherPrototype> prototypeList;

        public bool autoSelectCamera = true;
        public GPUInstancerCameraData cameraData = new GPUInstancerCameraData(null);

        public bool useFloatingOriginHandler = false;
        public bool applyFloatingOriginRotationAndScale = false;
        public Transform floatingOriginTransform;
        [NonSerialized]
        public GPUInstancerFloatingOriginHandler floatingOriginHandler;

        [NonSerialized]
        public List<GPUInstancerRuntimeData> runtimeDataList;
        [NonSerialized]
        public Bounds instancingBounds;

        public bool isFrustumCulling = true;
        public bool isOcclusionCulling = true;
        public float minCullingDistance = 0;

        protected GPUInstancerSpatialPartitioningData<GPUInstancerCell> spData;

        public static List<GPUInstancerManager> activeManagerList;
        public static bool showRenderedAmount;

        protected static ComputeShader _cameraComputeShader;
        protected static ComputeShader _cameraComputeShaderVR;
        protected static int[] _cameraComputeKernelIDs;

        protected static ComputeShader _visibilityComputeShader;
        protected static int[] _instanceVisibilityComputeKernelIDs;

        protected static ComputeShader _bufferToTextureComputeShader;
        protected static int _bufferToTextureComputeKernelID;
        protected static int _bufferToTextureCrossFadeComputeKernelID;

        protected static ComputeShader _argsBufferComputeShader;
        protected static int _argsBufferDoubleInstanceCountComputeKernelID;

#if UNITY_EDITOR
        public List<GPUInstancerPrototype> selectedPrototypeList;
        [NonSerialized]
        public GPUInstancerEditorSimulator gpuiSimulator;
        public bool isPrototypeTextMode = false;

        public bool showSceneSettingsBox = true;
        public bool showPrototypeBox = true;
        public bool showAdvancedBox = false;
        public bool showHelpText = false;
        public bool showDebugBox = true;
        public bool showGlobalValuesBox = true;
        public bool showRegisteredPrefabsBox = true;
        public bool showPrototypesBox = true;

        public bool keepSimulationLive = false;
        public bool updateSimulation = true;
#endif

        public class GPUIThreadData
        {
            public Thread thread;
            public object parameter;
        }
        public static int maxThreads = 3;
        public readonly List<Thread> activeThreads = new List<Thread>();
        public readonly Queue<GPUIThreadData> threadStartQueue = new Queue<GPUIThreadData>();
        public readonly Queue<Action> threadQueue = new Queue<Action>();

        // Tree variables
        public static int lastTreePositionUpdate;
        public static GameObject treeProxyParent;
        public static Dictionary<GameObject, Transform> treeProxyList; // Dict[TreePrefab, TreeProxyGO]

        // Time management
        public static int lastDrawCallFrame;
        public static float lastDrawCallTime;
        public static float timeSinceLastDrawCall;

        // Global Wind
        protected static Vector4 _windVector = Vector4.zero;

        [NonSerialized]
        protected bool isInitial = true;

        [NonSerialized]
        public bool isInitialized = false;

#if UNITY_EDITOR && UNITY_2017_2_OR_NEWER
        [NonSerialized]
        public PlayModeStateChange playModeState;
#endif
        [NonSerialized]
        public bool isQuiting = false;
        [NonSerialized]
        public Dictionary<GPUInstancerPrototype, GPUInstancerRuntimeData> runtimeDataDictionary;

        public LayerMask layerMask = ~0;
        public bool lightProbeDisabled = false;

        public GPUDrivenInstanceBatcher(Mesh mesh, Material material)
        {

        }
    }
}
