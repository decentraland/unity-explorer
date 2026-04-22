using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Character.Components;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
namespace DCL.SpringBones
{
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(SpringBoneRegistrationSystem))]
    [UpdateBefore(typeof(StartAvatarMatricesCalculationSystem))]
    public partial class SpringBonesSimulationSystem : BaseUnityLoopSystem
    {
        private readonly SpringBoneService springBoneService;
        private readonly SpringBoneSimulationSettings simulationSettings;
        private readonly List<(float sqrDistance, List<SpringBoneSlot> slots)> avatarDistances = new ();
        private bool wasEnabled = true;

        internal SpringBonesSimulationSystem(World world, SpringBoneService springBoneService, SpringBoneSimulationSettings simulationSettings) : base(world)
        {
            this.springBoneService = springBoneService;
            this.simulationSettings = simulationSettings;
        }

        protected override void Update(float t)
        {
            if (!simulationSettings.SimulationEnabled)
            {
                if (wasEnabled)
                {
                    springBoneService.DeactivateAllSlots();
                    springBoneService.PrepareSimulation();
                    wasEnabled = false;
                }

                SyncParentBonesQuery(World);
                return;
            }
            wasEnabled = true;

            avatarDistances.Clear();
            CollectRemoteAvatarDistancesQuery(World);
            CollectPlayerAvatarDistancesQuery(World);
            AlwaysSimulateNonPartitionedQuery(World);

            avatarDistances.Sort(static (a, b) => a.sqrDistance.CompareTo(b.sqrDistance));

            springBoneService.DeactivateAllSlots();

            int count = Math.Min(simulationSettings.MaxSimulatedAvatars, avatarDistances.Count);

            for (int i = 0; i < count; i++)
            {
                var slots = avatarDistances[i].slots;

                for (int s = 0; s < slots.Count; s++)
                    springBoneService.SetSlotActive(slots[s].SlotIndex, true);
            }

            SyncParentBonesQuery(World);

            springBoneService.PrepareSimulation();
            springBoneService.Simulate(t);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PlayerComponent))]
        private void CollectRemoteAvatarDistances(ref SpringBoneRegistrationComponent registration, ref PartitionComponent partition)
        {
            if (registration.Slots == null || registration.Slots.Count == 0) return;
            if (partition.IsBehind) return;

            avatarDistances.Add((partition.RawSqrDistance, registration.Slots));
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void CollectPlayerAvatarDistances(ref SpringBoneRegistrationComponent registration)
        {
            if (registration.Slots == null || registration.Slots.Count == 0) return;
            avatarDistances.Add((0f, registration.Slots));
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PartitionComponent), typeof(PlayerComponent))]
        private void AlwaysSimulateNonPartitioned(ref SpringBoneRegistrationComponent registration)
        {
            if (registration.Slots == null || registration.Slots.Count == 0) return;

            // Preview/UI avatars: no partition, always simulate with top priority
            avatarDistances.Add((0f, registration.Slots));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SyncParentBones(ref SpringBoneRegistrationComponent registration)
        {
            if (registration.Slots == null) return;

            foreach (SpringBoneSlot slot in registration.Slots)
            {
                slot.WearableParent.SetPositionAndRotation(slot.AvatarParent.position, slot.AvatarParent.rotation);

                if (springBoneService.IsSlotActive(slot.SlotIndex))
                {
                    springBoneService.SetParentData(slot.SlotIndex,
                        slot.AvatarParent.rotation, slot.AvatarParent.localToWorldMatrix);
                }
            }
        }
    }
}
