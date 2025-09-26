using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.PluginSystem.Global;
using DCL.SmartWearables;
using ECS.SceneLifeCycle;
using PortableExperiences.Controller;
using Runtime.Wearables;

namespace DCL.PluginSystem.SmartWearables
{
    public class SmartWearablesGlobalPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly WearableStorage wearableStorage;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly IScenesCache scenesCache;
        private readonly SmartWearableCache smartWearableCache;

        public SmartWearablesGlobalPlugin(WearableStorage wearableStorage, IBackpackEventBus backpackEventBus, IPortableExperiencesController portableExperiencesController, IScenesCache scenesCache, SmartWearableCache smartWearableCache)
        {
            this.wearableStorage = wearableStorage;
            this.backpackEventBus = backpackEventBus;
            this.portableExperiencesController = portableExperiencesController;
            this.scenesCache = scenesCache;
            this.smartWearableCache = smartWearableCache;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SmartWearableSystem.InjectToWorld(ref builder, wearableStorage, smartWearableCache, backpackEventBus, portableExperiencesController, scenesCache);
        }
    }
}
