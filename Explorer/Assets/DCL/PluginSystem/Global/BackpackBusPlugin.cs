using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using System;

namespace DCL.PluginSystem.Global
{
    public class BackpackBusPlugin : IDCLGlobalPluginWithoutSettings
    {
        public BackpackBusPlugin(
            IWearableCatalog wearableCatalog,
            BackpackCommandBus commandBus,
            BackpackEventBus eventBus,
            IBackpackEquipStatusController backpackEquipStatusController)
        {
            BackpackBusController busController = new BackpackBusController(wearableCatalog, eventBus, commandBus, backpackEquipStatusController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }
    }
}
