/*
 * Unity does not provide a way to record GC pressure in Performance Testing.
 * Despite it exposes `.GC` method. Thus it measures only count of calls:
 * https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/manual/measure-method.html
 * > GC() - if specified, measures the total number of Garbage Collection allocation calls.
 *
 * The package has custom markers to capture none-predefined values.
 * But Unity's Profiler won't provide API to get GC memory allocated per frame
 */

using System;
using System.Threading;
using UnityEngine;
using Unity.Profiling;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRunner.Scene.ExceptionsHandling;
using CrdtEcsBridge.PoolsProviders;

namespace SceneRuntime.Editor
{
    public class EngineAPIWrapperEditorPerformanceTest : MonoBehaviour
    {
        public enum Mode
        {
            NEW,
            LEGACY,
        }

        public static readonly ProfilerMarker Marker = new ("EngineAPIWrapperMeasure");

        [SerializeField]
        private int arraySize = 4096;
        [SerializeField]
        private int iterations = 1024;
        [SerializeField]
        private Mode mode = default(Mode);

        private EngineApiWrapper target = null!; // setup in Start()
        private byte[] array = Array.Empty<byte>();
        private TestArray testArray = null!;
        private IInstancePoolsProvider instancePoolsProvider = null!;

        private void Start()
        {
            IEngineApi api = new IEngineApi.Fake();
            ISceneData sceneData = new ISceneData.Fake(); 
            ISceneExceptionsHandler exceptionsHandler = new RethrowSceneExceptionsHandler();
            CancellationTokenSource disposeCts = new CancellationTokenSource();

            // Setup logic
            target = new EngineApiWrapper(api, sceneData, exceptionsHandler, disposeCts);
            instancePoolsProvider = InstancePoolsProvider.Create();
        }

        private void Update()
        {
            // Realloc on param's change
            if (arraySize != array.Length)
            {
                byte[] array = new byte[arraySize];
                testArray = new TestArray(array);
            }

            using var _ = Marker.Auto();
            for (int i = 0; i < iterations; i++)
                switch (mode)
                {
                    case Mode.NEW:
                        target.SendToRendererTest(testArray);
                        break;
                    case Mode.LEGACY:
                        target.SendToRendererTestLegacy(testArray, instancePoolsProvider);
                        break;
                }
        }
    }
}
