using Arch.SystemGroups;
using DCL.Backpack.BackpackBus;
using DCL.PluginSystem.Global;

namespace DCL.PluginSystem.World
{
    /// <summary>
    /// Injects systems that are needed for the Smart Wearables feature.
    ///
    /// - SmartWearableSystem listens to backpack events to initiate the loading flow of smart wearable scenes
    /// </summary>
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
