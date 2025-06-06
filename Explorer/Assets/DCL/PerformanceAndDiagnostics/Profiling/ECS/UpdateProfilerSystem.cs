using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner;
using SceneRunner.Scene;
using System.Net.NetworkInformation;

namespace DCL.Profiling.ECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public sealed partial class UpdateProfilerSystem : BaseUnityLoopSystem
    {
        private readonly IProfiler profiler;
        private readonly IScenesCache scenesCache;

        private readonly ulong startBytesSent;
        private readonly ulong startBytesReceived;
        private ulong prevBytesSent;
        private ulong prevBytesReceived;
        private ulong lastSecondBytesSent;
        private ulong lastSecondBytesReceived;
        private float lastBandwidthCheck;

        private UpdateProfilerSystem(World world, IProfiler profiler, IScenesCache scenesCache)
            : base(world)
        {
            this.profiler = profiler;
            this.scenesCache = scenesCache;

            foreach (NetworkInterface? ns in NetworkInterface.GetAllNetworkInterfaces())
                if (ns is { NetworkInterfaceType: NetworkInterfaceType.Wireless80211, OperationalStatus: OperationalStatus.Up })
                {
                    var netStats = ns.GetIPv4Statistics();
                    startBytesSent = prevBytesSent = lastSecondBytesSent = (ulong)netStats.BytesSent;
                    startBytesReceived = prevBytesReceived = lastSecondBytesReceived = (ulong)netStats.BytesReceived;
                    lastBandwidthCheck = UnityEngine.Time.unscaledTime;
                    break;
                }
        }

        protected override void Update(float t)
        {
            profiler.AllScenesTotalHeapSize = 0ul;
            profiler.AllScenesTotalHeapSizeExecutable = 0ul;
            profiler.AllScenesTotalPhysicalSize = 0ul;
            profiler.AllScenesUsedHeapSize = 0ul;
            profiler.AllScenesHeapSizeLimit = 0ul;
            profiler.AllScenesTotalExternalSize = 0ul;
            profiler.ActiveEngines = 0;

            SumSceneRuntimeHeapInfosQuery(World);

            if (profiler.ActiveEngines > 0)
                profiler.AllScenesHeapSizeLimit /= (ulong)profiler.ActiveEngines;

            profiler.CurrentSceneHasStats = false;

            if (scenesCache is { CurrentScene: { SceneStateProvider: { IsCurrent: true } } })
            {
                var scene = (SceneFacade)scenesCache.CurrentScene;
                var heapInfo = scene.runtimeInstance.RuntimeHeapInfo;

                if (heapInfo != null)
                {
                    profiler.CurrentSceneTotalHeapSize = heapInfo.TotalHeapSize;
                    profiler.CurrentSceneTotalHeapSizeExecutable = heapInfo.TotalHeapSizeExecutable;
                    profiler.CurrentSceneUsedHeapSize = heapInfo.UsedHeapSize;
                    profiler.CurrentSceneHasStats = true;
                }
            }

#if ENABLE_PROFILER
            if (!UnityEngine.Profiling.Profiler.enabled)
                return;

            if (UnityEngine.Profiling.Profiler.IsCategoryEnabled(JavaScriptProfilerCounters.CATEGORY))
            {
                JavaScriptProfilerCounters.TOTAL_HEAP_SIZE.Sample(profiler.AllScenesTotalHeapSize);
                JavaScriptProfilerCounters.TOTAL_HEAP_SIZE_EXECUTABLE.Sample(profiler.AllScenesTotalHeapSizeExecutable);
                JavaScriptProfilerCounters.TOTAL_PHYSICAL_SIZE.Sample(profiler.AllScenesTotalPhysicalSize);
                JavaScriptProfilerCounters.USED_HEAP_SIZE.Sample(profiler.AllScenesUsedHeapSize);
                JavaScriptProfilerCounters.TOTAL_EXTERNAL_SIZE.Sample(profiler.AllScenesTotalExternalSize);
                JavaScriptProfilerCounters.ACTIVE_ENGINES.Sample(profiler.ActiveEngines);
            }

            if (UnityEngine.Profiling.Profiler.IsCategoryEnabled(NetworkProfilerCounters.CATEGORY))
            {
                foreach (NetworkInterface? ns in NetworkInterface.GetAllNetworkInterfaces())
                    if (ns is { NetworkInterfaceType: NetworkInterfaceType.Wireless80211, OperationalStatus: OperationalStatus.Up })
                    {
                        var netStats = ns.GetIPv4Statistics();
                        var currentSent = (ulong)netStats.BytesSent;
                        var currentReceived = (ulong)netStats.BytesReceived;

                        NetworkProfilerCounters.WIFI_IPV4_BYTES_SENT.Value = currentSent - startBytesSent;
                        NetworkProfilerCounters.WIFI_IPV4_BYTES_RECEIVED.Value = currentReceived - startBytesReceived;

                        NetworkProfilerCounters.WIFI_IPV4_BYTES_FRAME_SENT.Value = currentSent - prevBytesSent;
                        NetworkProfilerCounters.WIFI_IPV4_BYTES_FRAME_RECEIVED.Value = currentReceived - prevBytesReceived;

                        prevBytesSent = currentSent;
                        prevBytesReceived = currentReceived;

                        if (UnityEngine.Time.unscaledTime - lastBandwidthCheck > 1f)
                        {
                            lastBandwidthCheck = UnityEngine.Time.unscaledTime;

                            NetworkProfilerCounters.WIFI_IPV4_MBPS_SENT.Value = (currentSent - lastSecondBytesSent) * 8f / 1_000_000f;
                            NetworkProfilerCounters.WIFI_IPV4_MBPS_RECEIVED.Value = (currentReceived - lastSecondBytesReceived) * 8f / 1_000_000f;
                            lastSecondBytesSent = currentSent;
                            lastSecondBytesReceived = currentReceived;
                        }
                    }
            }
#endif
        }

        [Query]
        private void SumSceneRuntimeHeapInfos(ISceneFacade scene0)
        {
            var scene = (SceneFacade)scene0;
            var heapInfo = scene.runtimeInstance.RuntimeHeapInfo;

            if (heapInfo != null)
            {
                profiler.AllScenesTotalHeapSize += heapInfo.TotalHeapSize;
                profiler.AllScenesTotalHeapSizeExecutable += heapInfo.TotalHeapSizeExecutable;
                profiler.AllScenesTotalPhysicalSize += heapInfo.TotalPhysicalSize;
                profiler.AllScenesUsedHeapSize += heapInfo.UsedHeapSize;
                profiler.AllScenesHeapSizeLimit += heapInfo.HeapSizeLimit;
                profiler.AllScenesTotalExternalSize += heapInfo.TotalExternalSize;
                profiler.ActiveEngines += 1;
            }
        }
    }
}
