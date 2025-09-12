using Arch.SystemGroups;
using DCL.Backpack.BackpackBus;
using DCL.PluginSystem.Global;
using DCL.SmartWearables;

namespace DCL.PluginSystem.SmartWearables
{
    public class SmartWearablesGlobalPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IBackpackEventBus backpackEventBus;

        public SmartWearablesGlobalPlugin(IBackpackEventBus backpackEventBus)
        {
            this.backpackEventBus = backpackEventBus;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SmartWearableSystem.InjectToWorld(ref builder, backpackEventBus);
        }
    }
}
