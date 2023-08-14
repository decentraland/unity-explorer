using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using ECS.Profiling;
using ECS.Profiling.Systems;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class ProfilingPlugin : IDCLGlobalPlugin<ProfilingPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IProfilingProvider profilingProvider;

        private ProvidedInstance<ProfilingView> profilingView;

        public ProfilingPlugin(IAssetsProvisioner assetsProvisioner, IProfilingProvider profilingProvider)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.profilingProvider = profilingProvider;
        }

        public async UniTask Initialize(Settings settings, CancellationToken ct)
        {
            profilingView = await assetsProvisioner.ProvideInstance(settings.profilingViewRef, ct: ct);
        }

        public void Dispose()
        {
            profilingView.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            ProfilingSystem.InjectToWorld(ref builder, profilingProvider, profilingView.Value);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [Serializable]
            public class ProfilingViewRef : ComponentReference<ProfilingView>
            {
                public ProfilingViewRef(string guid) : base(guid) { }
            }

            [field: Header(nameof(ProfilingPlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField] internal ProfilingViewRef profilingViewRef { get; private set; }
        }
    }
}
