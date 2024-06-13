using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using SceneRunner.Debugging.Hub;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class WorldInfoPlugin : IDCLGlobalPlugin
    {
        private readonly IWorldInfoHub worldInfoHub;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        public WorldInfoPlugin(IWorldInfoHub worldInfoHub, IDebugContainerBuilder debugContainerBuilder)
        {
            this.worldInfoHub = worldInfoHub;
            this.debugContainerBuilder = debugContainerBuilder;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            debugContainerBuilder
               .AddWidget("World Info");
               // .AddControl(
               //      new DebugHintDef("Some text"),
               //      new DebugTextFieldDef(new ElementBinding<string>("None"))
               //  );

            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            //ignore
        }

        public void Dispose()
        {
            //ignore
        }
    }
}
