using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Playground;
using ECS.Abstract;
using UnityEngine;

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
            UpdateTimeComponentQuery(World);
        }

        [Query]
        // [All(typeof(PBTimeComponent))]
        private void UpdateTimeComponent(CRDTEntity sdkEntity, ref PBSyncedClock pbTween)
        {
            Debug.Log($"VVV {pbTween.SyncedTimestamp} | {pbTween.Status}");
            ecsToCRDTWriter.PutMessage<PBSyncedClock, ulong>(
                static (component, currentServerTime) =>
                {
                    component.SyncedTimestamp = currentServerTime;
                    component.Status = SyncStatus.SsSynchronized;
                }, sdkEntity,
                33333
                //ntpTimeService.ServerTimeMs

                );
        }
    }
}
