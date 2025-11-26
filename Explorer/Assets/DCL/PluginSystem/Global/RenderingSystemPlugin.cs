using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.PluginSystem.Global
{
    public class RenderingSystemPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IDebugContainerBuilder debugContainerBuilder;

        public RenderingSystemPlugin(IDebugContainerBuilder debugContainerBuilder)
        {
            this.debugContainerBuilder = debugContainerBuilder;
            //
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            var version = new ElementBinding<string>(urpAsset.gpuResidentDrawerMode.ToString());
            debugContainerBuilder.TryAddWidget(IDebugContainerBuilder.Categories.RENDERING)
                                 .AddCustomMarker("GRD State:", version)
                                 .AddControl(
                                      new DebugConstLabelDef("Enabled GPU Resident Drawer"),
                                      new DebugButtonDef("GRD", EnableResidentDrawer)
                                  );

            void EnableResidentDrawer()
            {
                if (urpAsset.gpuResidentDrawerMode == GPUResidentDrawerMode.Disabled)
                {
                    urpAsset.gpuResidentDrawerMode = GPUResidentDrawerMode.InstancedDrawing;
                }
                else if (urpAsset.gpuResidentDrawerMode == GPUResidentDrawerMode.InstancedDrawing)
                {
                    urpAsset.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
                }
            }

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {

        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {



            //RenderingSystem.InjectToWorld();

            // UpdateProfilerSystem.InjectToWorld(ref builder, profiler, scenesCache);
            //
            // DebugViewProfilingSystem.InjectToWorld(ref builder, realmData, profiler, memoryBudget,
            //     debugContainerBuilder, dclVersion, adaptivePhysicsSettings, sceneLoadingLimit);
        }


    }
}
