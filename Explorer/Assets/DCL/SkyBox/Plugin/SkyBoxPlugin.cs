using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SkyBox.Rendering;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Utility;

namespace DCL.SkyBox
{
    public class SkyBoxPlugin : IDCLGlobalPlugin<SkyBoxPlugin.Settings>
    {
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly SkyBoxSceneData sceneData;

        private TimeOfDayRenderingModel? featureModel;

        public SkyBoxPlugin(IDebugContainerBuilder debugContainerBuilder, SkyBoxSceneData sceneData)
        {
            this.debugContainerBuilder = debugContainerBuilder;
            this.sceneData = sceneData;
        }

        public void Dispose()
        {
            // Nothing to do
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            // Update the model from the system
            TimeOfDaySystem.InjectToWorld(ref builder, debugContainerBuilder, featureModel, sceneData.DirectionalLight);
        }

        public UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            featureModel = settings.ForwardRenderer.FindRendererFeature<DCL_RenderFeature_ProceduralSkyBox>()?.RenderingModel;
            return UniTask.CompletedTask;
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(SkyBoxPlugin))] [field: Space]
            [field: SerializeField]
            public UniversalRendererData ForwardRenderer { get; private set; } = null!;
        }
    }
}
