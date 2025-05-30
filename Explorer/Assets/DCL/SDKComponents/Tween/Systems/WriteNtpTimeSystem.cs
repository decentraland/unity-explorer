using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Playground;
using ECS.Abstract;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    // [UpdateBefore(typeof(TweenUpdaterSystem))]
    // [LogCategory(ReportCategory.LogCategory)]
    public partial class WriteNtpTimeSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly INtpTimeService ntpTimeService;

        public WriteNtpTimeSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, INtpTimeService ntpTimeService) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.ntpTimeService = ntpTimeService;
        }

        protected override void Update(float t)
        {
            if(!ntpTimeService.IsSynced) return;

            ecsToCRDTWriter.PutMessage<PBSyncedClock, ulong>(static (component, currentServerTime) =>
                {
                    component.SyncedTimestamp = currentServerTime;
                    component.Status = SyncStatus.SsSynchronized;
                }, SpecialEntitiesID.SCENE_ROOT_ENTITY,
                ntpTimeService.ServerTimeMs
                );
        }
    }
}
