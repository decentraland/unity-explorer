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
        private readonly int maxSimulatedAvatars;
        private readonly List<(float sqrDistance, List<int> slotIndices)> avatarDistances = new ();

        internal SpringBonesSimulationSystem(World world, SpringBoneService springBoneService, int maxSimulatedAvatars) : base(world)
        {
            this.springBoneService = springBoneService;
            this.maxSimulatedAvatars = maxSimulatedAvatars;
        }

        protected override void Update(float t)
        {

            // 1. Collect distances for all avatars with spring bones
            avatarDistances.Clear();
            CollectRemoteAvatarDistancesQuery(World);
            CollectPlayerAvatarDistancesQuery(World);
            AlwaysSimulateNonPartitionedQuery(World);

            // 2. Sort by distance ascending
            avatarDistances.Sort(static (a, b) => a.sqrDistance.CompareTo(b.sqrDistance));

            // 3. Mark all slots inactive, then activate nearest N
            springBoneService.DeactivateAllSlots();

            int count = Math.Min(maxSimulatedAvatars, avatarDistances.Count);

            for (int i = 0; i < count; i++)
            {
                var slotIndices = avatarDistances[i].slotIndices;

                for (int s = 0; s < slotIndices.Count; s++)
                    springBoneService.SetSlotActive(slotIndices[s], true);
            }

            // 4. Sync parent bones (transform sync for all, SetParentData only for active)
            SyncParentBonesQuery(World);

            // 5. Handle inactive→active transitions, then simulate
            springBoneService.PrepareSimulation();
            springBoneService.Simulate(t);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PlayerComponent))]
        private void CollectRemoteAvatarDistances(ref SpringBoneRegistrationComponent registration, ref PartitionComponent partition)
        {
            if (registration.SlotIndices == null || registration.SlotIndices.Count == 0) return;

            // Cull avatars behind camera
            if (partition.IsBehind) return;

            avatarDistances.Add((partition.RawSqrDistance, registration.SlotIndices));
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void CollectPlayerAvatarDistances(ref SpringBoneRegistrationComponent registration)
        {
            if (registration.SlotIndices == null || registration.SlotIndices.Count == 0) return;
            avatarDistances.Add((0f, registration.SlotIndices));
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PartitionComponent), typeof(PlayerComponent))]
        private void AlwaysSimulateNonPartitioned(ref SpringBoneRegistrationComponent registration)
        {
            if (registration.SlotIndices == null || registration.SlotIndices.Count == 0) return;

            // Preview/UI avatars: no partition, always simulate with top priority
            avatarDistances.Add((0f, registration.SlotIndices));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SyncParentBones(ref SpringBoneRegistrationComponent registration)
        {
            for (int i = 0; i < registration.SyncPairs.Count; i++)
            {
                var (wearableParent, avatarParent) = registration.SyncPairs[i];

                // Always sync transform so rest-pose bones follow avatar skeleton
                wearableParent.SetPositionAndRotation(avatarParent.position, avatarParent.rotation);

                // Only feed parent data to simulation for active slots
                if (i < registration.SlotIndices.Count)
                {
                    int slotIndex = registration.SlotIndices[i];

                    if (springBoneService.IsSlotActive(slotIndex))
                    {
                        springBoneService.SetParentData(slotIndex,
                            avatarParent.rotation, avatarParent.localToWorldMatrix);
                    }
                }
            }
        }
    }
}
