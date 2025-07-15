using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.PluginSystem.Global;
using DCL.SDKComponents.LightSource.Systems;
using System;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class LightSourceDebugPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly Arch.Core.World globalWorld;
        private Entity debugStateEntity;

        public LightSourceDebugPlugin(IDebugContainerBuilder debugContainerBuilder, Arch.Core.World globalWorld)
        {
            this.debugContainerBuilder = debugContainerBuilder;
            this.globalWorld = globalWorld;
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
