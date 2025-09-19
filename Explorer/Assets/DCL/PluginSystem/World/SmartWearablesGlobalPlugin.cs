using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.PluginSystem.Global;
using DCL.SmartWearables;
using PortableExperiences.Controller;

namespace DCL.PluginSystem.SmartWearables
{
    public class SmartWearablesGlobalPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly WearableStorage wearableStorage;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IPortableExperiencesController portableExperiencesController;

        public SmartWearablesGlobalPlugin(WearableStorage wearableStorage, IBackpackEventBus backpackEventBus, IPortableExperiencesController portableExperiencesController)
        {
            this.wearableStorage = wearableStorage;
            this.backpackEventBus = backpackEventBus;
            this.portableExperiencesController = portableExperiencesController;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SmartWearableSystem.InjectToWorld(ref builder, wearableStorage, backpackEventBus, portableExperiencesController);
        }
    }
}
