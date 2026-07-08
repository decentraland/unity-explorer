using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.PluginSystem.Global;
using DCL.SDKComponents.LightSource;
using DCL.SDKComponents.LightSource.Systems;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class LightSourceDebugPlugin : IDCLGlobalPluginWithoutSettings
    {
        /// <summary>
        ///     The biggest LOD count defined across the quality presets. Fields beyond the current preset's
        ///     LOD count display 0 and ignore edits.
        /// </summary>
        private const int MAX_LOD_COUNT = 2;

        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly Arch.Core.World globalWorld;
        private readonly LightSourceSettings lightSourceSettings;
        private Entity debugStateEntity;

        public LightSourceDebugPlugin(IDebugContainerBuilder debugContainerBuilder, Arch.Core.World globalWorld, LightSourceSettings lightSourceSettings)
        {
            this.debugContainerBuilder = debugContainerBuilder;
            this.globalWorld = globalWorld;
            this.lightSourceSettings = lightSourceSettings;
        }

        public UniTask InitializeAsync(object settings, CancellationToken ct)
        {
            debugStateEntity = globalWorld.Create();
            var debugState = LightSourceDebugState.New();

            globalWorld.Add(debugStateEntity, debugState);

            CreateDebugWidget(debugState);

            return UniTask.CompletedTask;
        }

        private void CreateDebugWidget(in LightSourceDebugState debugState)
        {
            var widget = debugContainerBuilder?.TryAddWidget("Light Sources");

            widget?.AddToggleField("Lights", evt =>
                {
                    UpdateDebugState(s =>
                    {
                        s.LightsEnabled = evt.newValue;
                        return s;
                    });
                },
                debugState.ShadowsEnabled);

            widget?.AddToggleField("Shadows", evt =>
                {
                    UpdateDebugState(s =>
                    {
                        s.ShadowsEnabled = evt.newValue;
                        return s;
                    });
                },
                debugState.ShadowsEnabled);

            widget?.AddToggleField("Point Light Shadows", evt =>
                {
                    UpdateDebugState(s =>
                    {
                        s.PointLightShadowsEnabled = evt.newValue;
                        return s;
                    });
                },
                debugState.PointLightShadowsEnabled);

            if (widget != null)
                AddLodDistanceFields(widget);
        }

        private void AddLodDistanceFields(DebugWidgetBuilder widget)
        {
            var refreshUiValues = new List<Action>(MAX_LOD_COUNT * 2);

            AddLodDistanceFields(widget, "Spot", lightSourceSettings.SpotLightsLods, refreshUiValues);
            AddLodDistanceFields(widget, "Point", lightSourceSettings.PointLightsLods, refreshUiValues);

            // Quality presets replace the LOD lists, so the fields must re-read the new values.
            QualitySettings.activeQualityLevelChanged += (_, _) =>
            {
                foreach (Action refresh in refreshUiValues)
                    refresh();
            };
        }

        private static void AddLodDistanceFields(DebugWidgetBuilder widget, string lightType, List<LightSourceSettings.LodSettings> lods, List<Action> refreshUiValues)
        {
            for (var i = 0; i < MAX_LOD_COUNT; i++)
            {
                int lod = i;
                var binding = new ElementBinding<float>(GetLodDistance(lods, lod), evt => SetLodDistance(lods, lod, evt.newValue));
                refreshUiValues.Add(() => binding.Value = GetLodDistance(lods, lod));
                widget.AddFloatField($"{lightType} LOD{lod} Distance", binding);
            }
        }

        private static float GetLodDistance(List<LightSourceSettings.LodSettings> lods, int lod) =>
            lod < lods.Count ? lods[lod].Distance : 0f;

        private static void SetLodDistance(List<LightSourceSettings.LodSettings> lods, int lod, float distance)
        {
            if (lod >= lods.Count)
                return;

            LightSourceSettings.LodSettings lodSettings = lods[lod];
            lodSettings.Distance = distance;
            lods[lod] = lodSettings;
        }

        private void UpdateDebugState(Func<LightSourceDebugState, LightSourceDebugState> updateFunc)
        {
            var debugState = globalWorld.Get<LightSourceDebugState>(debugStateEntity);

            debugState = updateFunc.Invoke(debugState);

            globalWorld.Set(debugStateEntity, debugState);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }
    }
}
