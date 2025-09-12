using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.PluginSystem.Global;
using DCL.SmartWearables;

namespace DCL.PluginSystem.SmartWearables
{
    public class SmartWearablesGlobalPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly WearableStorage wearableStorage;

        private readonly IBackpackEventBus backpackEventBus;

        public SmartWearablesGlobalPlugin(WearableStorage wearableStorage, IBackpackEventBus backpackEventBus)
        {
            this.wearableStorage = wearableStorage;
            this.backpackEventBus = backpackEventBus;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SmartWearableSystem.InjectToWorld(ref builder, wearableStorage, backpackEventBus);
        }
    }
}
