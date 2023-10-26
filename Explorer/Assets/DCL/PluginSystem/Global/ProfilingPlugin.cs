using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities.Builders;
using ECS.Profiling;
using ECS.Profiling.Systems;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class ProfilingPlugin : IDCLGlobalPlugin<ProfilingPlugin.Settings>
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        public ProfilingPlugin(IProfilingProvider profilingProvider, IDebugContainerBuilder debugContainerBuilder)
        {
            this.profilingProvider = profilingProvider;
            this.debugContainerBuilder = debugContainerBuilder;
        }

        public UniTask InitializeAsync(Settings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ProfilingSystem.InjectToWorld(ref builder, profilingProvider, debugContainerBuilder);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(ProfilingPlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField] internal ProfilingViewRef profilingViewRef { get; private set; }

            [Serializable]
            public class ProfilingViewRef : ComponentReference<ProfilingView>
            {
                public ProfilingViewRef(string guid) : base(guid) { }
            }
        }

        public void Dispose() { }
    }
}
