using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Helpers;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PrepareWearableSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadDefaultWearablesSystem : BaseUnityLoopSystem
    {
        //TODO: How can I wait for this system to end?
        //Also, if this system fails, the avatar will be stuck in a loading state forever
        private readonly string CATALYST_URL;

        public LoadDefaultWearablesSystem(World world, string catalystURL) : base(world)
        {
            CATALYST_URL = catalystURL;
        }

        public override void Initialize()
        {
            base.Initialize();

            AssetPromise<WearableDTO[], GetWearableByPointersIntention>.Create(World,
                new GetWearableByPointersIntention
                {
                    //TODO: Should a prepare system be done for the catalyst url?
                    CommonArguments = new CommonLoadingArguments(CATALYST_URL),
                    Pointers = WearablesLiterals.DefaultWearables.GetDefaultWearables(),
                    StartAssetBundlesDownload = true,
                }, PartitionComponent.TOP_PRIORITY);
        }

        protected override void Update(float t)
        {
        }
    }
}
