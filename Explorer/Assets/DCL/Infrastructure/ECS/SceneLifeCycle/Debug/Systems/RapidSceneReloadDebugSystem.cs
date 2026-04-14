using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using SceneRunner.Scene;
using UnityEngine;
using ScenePromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.ISceneFacade, ECS.SceneLifeCycle.Components.GetSceneFacadeIntention>;

namespace ECS.SceneLifeCycle.Debug
{
    /// <summary>
    ///     Debug system that periodically force-unloads all loaded scenes to reproduce crashes
    ///     related to rapid scene loading and unloading cycles.
    ///     Scenes are unloaded via the same <see cref="DeleteEntityIntention"/> path used in production,
    ///     then reload naturally through the existing scene lifecycle systems.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadGlobalSystemGroup))]
    [LogCategory(ReportCategory.DEBUG)]
    public partial class RapidSceneReloadDebugSystem : BaseUnityLoopSystem
    {
        private readonly ElementBinding<float> intervalBinding;
        private readonly ElementBinding<string> statusBinding;
        private bool isEnabled;
        private float timer;
        private int cycleCount;
        private int lastDisplayedCycle = -1;
        private int lastDisplayedTenths = -1;

        private RapidSceneReloadDebugSystem(World world, DebugWidgetBuilder debugWidgetBuilder) : base(world)
        {
            intervalBinding = new ElementBinding<float>(3f);
            statusBinding = new ElementBinding<string>("Disabled");

            debugWidgetBuilder
                .AddToggleField("Rapid Reload", evt =>
                {
                    isEnabled = evt.newValue;
                    timer = 0f;
                    cycleCount = 0;
                    statusBinding.Value = isEnabled ? "Enabled - waiting..." : "Disabled";
                }, false)
                .AddFloatField("Interval (sec)", intervalBinding)
                .AddCustomMarker("Reload Status:", statusBinding);
        }

        protected override void Update(float t)
        {
            if (!isEnabled) return;

            timer += t;

            float interval = Mathf.Max(0.1f, intervalBinding.Value);
            float remaining = Mathf.Max(0f, interval - timer);
            int displayTenths = (int)(remaining * 10f);

            if (cycleCount != lastDisplayedCycle || displayTenths != lastDisplayedTenths)
            {
                lastDisplayedCycle = cycleCount;
                lastDisplayedTenths = displayTenths;
                statusBinding.Value = $"Cycle #{cycleCount} | Next: {remaining:F1}s";
            }

            if (timer < interval) return;

            timer = 0f;
            cycleCount++;

            // Force unload all currently-loaded scenes
            ForceUnloadLoadedScenesQuery(World);

            // Also abort any in-progress scene loads so they restart from scratch
            AbortLoadingScenesQuery(World);
        }

        [Query]
        [All(typeof(ISceneFacade), typeof(SceneLoadingState))]
        [None(typeof(DeleteEntityIntention), typeof(PortableExperienceComponent), typeof(SmartWearableId))]
        private void ForceUnloadLoadedScenes(in Entity entity, ref SceneLoadingState sceneLoadingState)
        {
            sceneLoadingState.VisualSceneState = VisualSceneState.UNINITIALIZED;
            sceneLoadingState.PromiseCreated = false;
            sceneLoadingState.FullQuality = false;
            World.Add(entity, new DeleteEntityIntention { DeferDeletion = true });
        }

        [Query]
        [All(typeof(ScenePromise), typeof(SceneLoadingState))]
        [None(typeof(ISceneFacade), typeof(DeleteEntityIntention), typeof(PortableExperienceComponent), typeof(SmartWearableId))]
        private void AbortLoadingScenes(in Entity entity, ref SceneLoadingState sceneLoadingState)
        {
            sceneLoadingState.VisualSceneState = VisualSceneState.UNINITIALIZED;
            sceneLoadingState.PromiseCreated = false;
            sceneLoadingState.FullQuality = false;
            World.Add(entity, new DeleteEntityIntention { DeferDeletion = true });
        }
    }
}
