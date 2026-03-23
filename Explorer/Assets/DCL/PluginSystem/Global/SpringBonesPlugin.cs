using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class SpringBonesPlugin : IDCLGlobalPlugin<SpringBonesSettings>
    {
        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public UniTask InitializeAsync(SpringBonesSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;
    }

    [Serializable]
    public class SpringBonesSettings : IDCLPluginSettings { }
}
