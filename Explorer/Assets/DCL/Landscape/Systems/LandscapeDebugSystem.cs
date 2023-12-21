using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Prioritization;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeDebugSystem : BaseUnityLoopSystem
    {
        private readonly ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings;

        public LandscapeDebugSystem(World world, IDebugContainerBuilder debugBuilder, ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;

            debugBuilder.AddWidget("Landscape")
                        .AddIntFieldWithConfirmation(realmPartitionSettings.Value.MaxLoadingDistanceInParcels, "Set Load Radius", OnLoadRadiusConfirm);
        }

        private void OnLoadRadiusConfirm(int value)
        {
            realmPartitionSettings.Value.MaxLoadingDistanceInParcels = value;
        }

        protected override void Update(float t) { }
    }
}
