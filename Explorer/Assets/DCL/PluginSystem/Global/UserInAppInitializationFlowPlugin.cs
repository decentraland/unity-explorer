using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class UserInAppInitializationFlowPlugin : IDCLGlobalPlugin
    {
        private readonly RealUserInitializationFlowController userInitializationFlow;

        public UserInAppInitializationFlowPlugin(RealUserInitializationFlowController userInitializationFlow)
        {
            this.userInitializationFlow = userInitializationFlow;
        }

        public void Dispose() { }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            userInitializationFlow.InjectToWorld(builder.World, in arguments.PlayerEntity);
    }
}
