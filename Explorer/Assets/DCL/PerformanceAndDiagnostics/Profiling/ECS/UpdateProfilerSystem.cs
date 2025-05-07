using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner;
using SceneRunner.Scene;

namespace DCL.Profiling.ECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public sealed partial class UpdateProfilerSystem : BaseUnityLoopSystem
    {
        private readonly IProfiler profiler;
        private readonly IScenesCache scenesCache;

        private UpdateProfilerSystem(World world, IProfiler profiler, IScenesCache scenesCache)
            : base(world)
        {
            this.profiler = profiler;
            this.scenesCache = scenesCache;
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
            JavaScriptProfilerCounters.TOTAL_HEAP_SIZE.Sample(profiler.AllScenesTotalHeapSize);
            JavaScriptProfilerCounters.TOTAL_HEAP_SIZE_EXECUTABLE.Sample(profiler.AllScenesTotalHeapSizeExecutable);
            JavaScriptProfilerCounters.TOTAL_PHYSICAL_SIZE.Sample(profiler.AllScenesTotalPhysicalSize);
            JavaScriptProfilerCounters.USED_HEAP_SIZE.Sample(profiler.AllScenesUsedHeapSize);
            JavaScriptProfilerCounters.TOTAL_EXTERNAL_SIZE.Sample(profiler.AllScenesTotalExternalSize);
            JavaScriptProfilerCounters.ACTIVE_ENGINES.Sample(profiler.ActiveEngines);
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
